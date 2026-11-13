using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhalePet;

public sealed class MemoItem : INotifyPropertyChanged
{
    private bool _done;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool Done
    {
        get => _done;
        set
        {
            if (_done == value) return;
            _done = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Done)));
        }
    }

    [JsonIgnore]
    public string CreatedAtDisplay => CreatedAt.ToString("MM-dd HH:mm");

    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class MemoStore
{
    private const string FileName = "memos.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string FilePath => PersistentStore.PrimaryPath(FileName);

    public static string BackupPath => PersistentStore.RoamingPath(FileName);

    public static List<MemoItem> Load()
    {
        foreach (var json in PersistentStore.ReadCandidates(FileName))
        {
            try
            {
                var memos = JsonSerializer.Deserialize<List<MemoItem>>(json, JsonOptions);
                if (memos is not null)
                {
                    return memos
                        .Where(memo => memo is not null && !string.IsNullOrWhiteSpace(memo.Text))
                        .OrderByDescending(memo => memo.CreatedAt)
                        .Take(100)
                        .ToList();
                }
            }
            catch
            {
                // Try the next copy.
            }
        }
        return new List<MemoItem>();
    }

    public static void Save(IEnumerable<MemoItem> memos)
    {
        var json = JsonSerializer.Serialize(memos.Take(100).ToList(), JsonOptions);
        PersistentStore.Save(FileName, json);
    }
}
