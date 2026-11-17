using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhalePet;

public sealed class ClipboardEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string CreatedAtDisplay => CreatedAt.ToString("MM-dd HH:mm");
}

/// <summary>Text-only clipboard history, stored with the same redundancy as settings.</summary>
public static class ClipboardStore
{
    public const int MaxEntries = 50;
    private const string FileName = "clipboard.json";

    /// <summary>Raised after the history has been changed by the app or a clipboard update.</summary>
    public static event EventHandler? Changed;

    public static void NotifyChanged() => Changed?.Invoke(null, EventArgs.Empty);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static List<ClipboardEntry> Load()
    {
        foreach (var json in PersistentStore.ReadCandidates(FileName))
        {
            try
            {
                var entries = JsonSerializer.Deserialize<List<ClipboardEntry>>(json, JsonOptions);
                if (entries is not null)
                {
                    return entries
                        .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Text))
                        .OrderByDescending(entry => entry.CreatedAt)
                        .Take(MaxEntries)
                        .ToList();
                }
            }
            catch
            {
                // Try the next copy.
            }
        }
        return new List<ClipboardEntry>();
    }

    public static void Save(IEnumerable<ClipboardEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries.Take(MaxEntries).ToList(), JsonOptions);
        PersistentStore.Save(FileName, json);
    }
}
