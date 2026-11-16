using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhalePet;

public sealed class MemoItem : INotifyPropertyChanged
{
    private string _text = "";
    private bool _done;
    private bool _selected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

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

    /// <summary>Transient selection used by the bulk-delete mode; never persisted.</summary>
    [JsonIgnore]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    [JsonIgnore]
    public string CreatedAtDisplay => CreatedAt.ToString("MM-dd HH:mm");

    public event PropertyChangedEventHandler? PropertyChanged;
}
