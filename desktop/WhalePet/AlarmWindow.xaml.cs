using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;

namespace WhalePet;

public partial class AlarmWindow : Window
{
    private readonly ObservableCollection<AlarmItem> _alarms;

    public List<AlarmItem> Alarms { get; private set; } = new();

    public AlarmWindow(IEnumerable<AlarmItem> alarms)
    {
        InitializeComponent();
        var settings = SettingsStore.Load();
        Background = ThemePalette.Surface(
            settings.ThemeColor,
            settings.BackgroundImageEnabled,
            settings.BackgroundImagePath,
            settings.BackgroundImageOpacity,
            0.16);
        var accent = ThemePalette.Parse(settings.ThemeColor, System.Windows.Media.Color.FromRgb(0x4D, 0x6B, 0xFE));
        Resources["AccentFill"] = ThemePalette.Brush(accent, 0.18);
        Resources["AccentBorder"] = ThemePalette.Brush(ThemePalette.Lighten(accent, 0.25), 0.85);
        SaveButton.Background = ThemePalette.Brush(accent);
        SaveButton.BorderBrush = ThemePalette.Brush(accent);
        _alarms = new ObservableCollection<AlarmItem>(alarms.Select(alarm => alarm.Clone()));
        AlarmList.ItemsSource = _alarms;
        _alarms.CollectionChanged += (_, _) => UpdateEmptyHint();
        UpdateEmptyHint();
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = _alarms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddAlarm(object sender, RoutedEventArgs e)
    {
        if (_alarms.Count >= 10)
        {
            ErrorText.Text = "最多 10 个闹钟。";
            return;
        }

        var start = DateTime.Now.AddMinutes(5);
        _alarms.Add(new AlarmItem
        {
            IsDaily = true,
            Start = start.ToString("HH:mm"),
            End = "",
            Text = "提醒",
        });
        ErrorText.Text = "";
        UpdateEmptyHint();
        AlarmScroll.ScrollToEnd();
    }

    private void OnDeleteAlarm(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AlarmItem alarm }) _alarms.Remove(alarm);
        ErrorText.Text = "";
        UpdateEmptyHint();
    }

    private void OnPickTime(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AlarmItem alarm }) return;
        var dialog = new AlarmTimePickerWindow(alarm) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        alarm.Date = dialog.SelectedDate;
        alarm.Start = dialog.SelectedStart;
        alarm.End = dialog.SelectedEnd;
        ErrorText.Text = "";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var today = DateTime.Today;
        var result = new List<AlarmItem>();
        foreach (var alarm in _alarms)
        {
            if (!AlarmItem.IsValidClock(alarm.Start))
            {
                ErrorText.Text = "开始时间格式不正确，请使用 HH:mm，例如 09:30。";
                return;
            }

            var hasEnd = !string.IsNullOrWhiteSpace(alarm.End);
            if (hasEnd && !AlarmItem.IsValidClock(alarm.End))
            {
                ErrorText.Text = "结束时间格式不正确，请使用 HH:mm，或留空。";
                return;
            }
            if (hasEnd && string.CompareOrdinal(alarm.End, alarm.Start) < 0)
            {
                ErrorText.Text = "结束时间不能早于开始时间（也可以留空）。";
                return;
            }

            if (!alarm.IsDaily)
            {
                if (!AlarmItem.IsValidDate(alarm.Date))
                {
                    ErrorText.Text = "请选择有效的提醒日期。";
                    return;
                }
                var date = DateTime.ParseExact(alarm.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (date.Date < today)
                {
                    ErrorText.Text = "提醒日期不能早于今天。";
                    return;
                }
            }

            var text = (alarm.Text ?? "").Trim();
            if (text.Length == 0)
            {
                ErrorText.Text = "提醒内容不能为空。";
                return;
            }

            var item = alarm.Clone();
            item.Text = text.Length > 60 ? text[..60] : text;
            result.Add(item);
        }

        Alarms = result;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
