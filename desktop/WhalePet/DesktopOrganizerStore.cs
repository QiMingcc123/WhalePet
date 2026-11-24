using System.IO;
using System.Text.Json;

namespace WhalePet;

public sealed class OrganizerItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string Category { get; set; } = "未分类";
}

public sealed class OrganizerData
{
    public List<string> Categories { get; set; } = new() { "未分类" };
    public string? DefaultCategory { get; set; } = "未分类";
    public List<OrganizerItem> Items { get; set; } = new();
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 520;
    public double Height { get; set; } = 420;
}

public static class DesktopOrganizerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private const string FileName = "desktop-organizer.json";

    public static OrganizerData Load()
    {
        foreach (var json in PersistentStore.ReadCandidates(FileName))
        {
            try
            {
                var data = JsonSerializer.Deserialize<OrganizerData>(json, JsonOptions);
                if (data is null) continue;
                data.Categories = (data.Categories ?? new List<string>())
                    .Select(name => (name ?? "").Trim())
                    .Where(name => name.Length is > 0 and <= 20)
                    .Distinct(StringComparer.Ordinal)
                    .Take(30)
                    .ToList();
                if (data.Categories.Count == 0)
                {
                    data.Categories.Add("未分类");
                }

                var defaultCategory = (data.DefaultCategory ?? "").Trim();
                if (defaultCategory.Length == 0 ||
                    !data.Categories.Contains(defaultCategory, StringComparer.Ordinal))
                {
                    defaultCategory = data.Categories[0];
                }
                data.DefaultCategory = defaultCategory;
                var defaultIndex = data.Categories.IndexOf(defaultCategory);
                if (defaultIndex > 0)
                {
                    data.Categories.RemoveAt(defaultIndex);
                    data.Categories.Insert(0, defaultCategory);
                }

                data.Items = (data.Items ?? new List<OrganizerItem>())
                    .Where(item => !string.IsNullOrWhiteSpace(item.TargetPath))
                    .Take(2000)
                    .ToList();
                foreach (var item in data.Items)
                {
                    item.Name = string.IsNullOrWhiteSpace(item.Name)
                        ? Path.GetFileNameWithoutExtension(item.TargetPath)
                        : item.Name.Trim();
                    if (!data.Categories.Contains(item.Category)) item.Category = defaultCategory;
                }
                if (!double.IsFinite(data.Width)) data.Width = 520;
                if (!double.IsFinite(data.Height)) data.Height = 420;
                data.Width = Math.Clamp(data.Width, 300, 1200);
                data.Height = Math.Clamp(data.Height, 300, 1000);
                return data;
            }
            catch
            {
                // Try the backup.
            }
        }
        return new OrganizerData();
    }

    public static void Save(OrganizerData data)
    {
        try
        {
            PersistentStore.Save(FileName, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Organizer persistence must not crash the pet.
        }
    }
}
