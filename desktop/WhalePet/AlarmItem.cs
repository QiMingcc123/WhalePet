using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace WhalePet;

/// <summary>
/// A reminder. An empty Date means "every day"; otherwise the alarm only fires
/// on that date (yyyy-MM-dd). Start is always set; End is optional and makes the
/// alarm fire once when the clock first enters the Start–End window.
/// </summary>
public sealed class AlarmItem : INotifyPropertyChanged
{
    private bool _enabled = true;
    private string _date = "";
    private string _start = "09:00";
    private string _end = "";
    private string _text = "提醒";
    private bool _popup = true;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            Notify(nameof(Enabled));
        }
    }

    public string Date
    {
        get => _date;
        set
        {
            var normalized = NormalizeDate(value);
            if (_date == normalized) return;
            _date = normalized;
            Notify(nameof(Date));
            Notify(nameof(DateDisplay));
            Notify(nameof(IsDaily));
        }
    }

    public string Start
    {
        get => _start;
        set
        {
            var normalized = NormalizeClock(value);
            if (normalized is null)
            {
                // Invalid input: refresh the editor back to the previous value.
                Notify(nameof(Start));
                return;
            }
            if (_start == normalized) return;
            _start = normalized;
            Notify(nameof(Start));
        }
    }

    public string End
    {
        get => _end;
        set
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0)
            {
                if (_end.Length == 0) return;
                _end = "";
                Notify(nameof(End));
                return;
            }

            var normalized = NormalizeClock(text);
            if (normalized is null)
            {
                Notify(nameof(End));
                return;
            }
            if (_end == normalized) return;
            _end = normalized;
            Notify(nameof(End));
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            Notify(nameof(Text));
        }
    }

    public bool Popup
    {
        get => _popup;
        set
        {
            if (_popup == value) return;
            _popup = value;
            Notify(nameof(Popup));
        }
    }

    [JsonIgnore]
    public bool IsDaily
    {
        get => string.IsNullOrEmpty(_date);
        set
        {
            if (value)
            {
                Date = "";
            }
            else if (string.IsNullOrEmpty(_date))
            {
                Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }
    }

    [JsonIgnore]
    public string DateDisplay => IsDaily ? "" : _date;

    public AlarmItem Clone() => new()
    {
        Enabled = Enabled,
        Date = Date,
        Start = Start,
        End = End,
        Text = Text,
        Popup = Popup,
    };

    /// <summary>
    /// Accepts 9, 09, 930, 0930, 9:5, 9:05, 9：05, 9点30, 9时30分 and returns HH:mm.
    /// Returns null for values that cannot be a real time.
    /// </summary>
    public static string? NormalizeClock(string? input)
    {
        var text = (input ?? "").Trim();
        if (text.Length == 0) return null;

        text = text.Replace('：', ':')
            .Replace('。', ':')
            .Replace('.', ':')
            .Replace("点", ":")
            .Replace("时", ":")
            .Replace("分", "")
            .Replace(" ", "")
            .Replace("\t", "");

        int hour;
        int minute;
        if (text.Contains(':'))
        {
            var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 2) return null;
            if (!int.TryParse(parts[0], out hour)) return null;
            minute = 0;
            if (parts.Length == 2 && !int.TryParse(parts[1], out minute)) return null;
        }
        else
        {
            if (text.Length == 0 || !text.All(char.IsDigit)) return null;
            switch (text.Length)
            {
                case 1:
                case 2:
                    hour = int.Parse(text);
                    minute = 0;
                    break;
                case 3:
                    hour = int.Parse(text[..1]);
                    minute = int.Parse(text[1..]);
                    break;
                case 4:
                    hour = int.Parse(text[..2]);
                    minute = int.Parse(text[2..]);
                    break;
                default:
                    return null;
            }
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59) return null;
        return $"{hour:00}:{minute:00}";
    }

    public static bool IsValidClock(string? value) => NormalizeClock(value) is not null;

    public static bool IsValidDate(string? value) =>
        DateTime.TryParseExact((value ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static string NormalizeDate(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) return "";
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return "";
        }
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
