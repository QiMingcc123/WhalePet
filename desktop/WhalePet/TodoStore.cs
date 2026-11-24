using System.IO;
using System.Text.Json;

namespace WhalePet;

public sealed class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Urgency { get; set; } = "Green";
    public string Status { get; set; } = "NotStarted";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class TodoData
{
    public List<TodoItem> Items { get; set; } = new();
    public List<string> TimeFilters { get; set; } = new();
    public List<string> UrgencyFilters { get; set; } = new();
    public DateTime? CustomStart { get; set; }
    public DateTime? CustomEnd { get; set; }
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 520;
    public double Height { get; set; } = 460;
}

public static class TodoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string FileName => "todos.json";

    public static TodoData Load()
    {
        foreach (var json in PersistentStore.ReadCandidates(FileName))
        {
            try
            {
                var data = JsonSerializer.Deserialize<TodoData>(json, JsonOptions);
                if (data is null) continue;
                data.Items = (data.Items ?? new List<TodoItem>())
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                    .Take(1000)
                    .ToList();
                foreach (var item in data.Items)
                {
                    item.Notes ??= "";
                    if (item.Notes.Length > 1000) item.Notes = item.Notes[..1000];
                }
                data.TimeFilters = (data.TimeFilters ?? new List<string>())
                    .Where(filter => filter is "今天" or "昨天" or "本周" or "上周" or "本月" or "上月" or "自定义")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                data.UrgencyFilters = (data.UrgencyFilters ?? new List<string>())
                    .Where(filter => TodoPalette.Urgencies.Any(entry => entry.Key == filter))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (!double.IsFinite(data.Width)) data.Width = 520;
                if (!double.IsFinite(data.Height)) data.Height = 460;
                data.Width = Math.Clamp(data.Width, 300, 1200);
                data.Height = Math.Clamp(data.Height, 300, 1000);
                return data;
            }
            catch
            {
                // Try the backup.
            }
        }
        return new TodoData();
    }

    public static void Save(TodoData data)
    {
        try
        {
            PersistentStore.Save(FileName, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Todo persistence must not crash the pet.
        }
    }
}
