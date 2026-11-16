using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WhalePet;

public partial class AlarmTimePickerWindow : Window
{
    private bool _ready;

    public string SelectedDate { get; private set; } = "";
    public string SelectedStart { get; private set; } = "09:00";
    public string SelectedEnd { get; private set; } = "";

    public AlarmTimePickerWindow(AlarmItem alarm)
    {
        InitializeComponent();

        var settings = SettingsStore.Load();
        Background = ThemePalette.Surface(
            settings.ThemeColor,
            settings.BackgroundImageEnabled,
            settings.BackgroundImagePath,
            settings.BackgroundImageOpacity,
            0.16);
        var accent = ThemePalette.Parse(settings.ThemeColor, Color.FromRgb(0x4D, 0x6B, 0xFE));
        OkButton.Background = ThemePalette.Brush(accent);
        OkButton.BorderBrush = ThemePalette.Brush(accent);

        var date = AlarmItem.IsValidDate(alarm.Date)
            ? DateTime.ParseExact(alarm.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DateTime.Today;

        YearBox.ItemsSource = Enumerable.Range(DateTime.Today.Year, 11).ToList();
        MonthBox.ItemsSource = Enumerable.Range(1, 12).ToList();
        HourBox.ItemsSource = Enumerable.Range(0, 24).ToList();
        MinuteBox.ItemsSource = Enumerable.Range(0, 60).ToList();
        EndHourBox.ItemsSource = Enumerable.Range(0, 24).ToList();
        EndMinuteBox.ItemsSource = Enumerable.Range(0, 60).ToList();

        YearBox.SelectedItem = date.Year;
        MonthBox.SelectedItem = date.Month;
        RepopulateDays(date.Day);

        var start = AlarmItem.NormalizeClock(alarm.Start) ?? "09:00";
        HourBox.SelectedItem = int.Parse(start[..2], CultureInfo.InvariantCulture);
        MinuteBox.SelectedItem = int.Parse(start[3..], CultureInfo.InvariantCulture);

        var end = AlarmItem.NormalizeClock(alarm.End) ?? "10:00";
        EndHourBox.SelectedItem = int.Parse(end[..2], CultureInfo.InvariantCulture);
        EndMinuteBox.SelectedItem = int.Parse(end[3..], CultureInfo.InvariantCulture);
        EndCheck.IsChecked = AlarmItem.NormalizeClock(alarm.End) is not null;
        DailyCheck.IsChecked = alarm.IsDaily;

        _ready = true;
        UpdateDateEnabled();
        UpdateEndEnabled();
    }

    private void RepopulateDays(int preferred)
    {
        var year = YearBox.SelectedItem is int y ? y : DateTime.Today.Year;
        var month = MonthBox.SelectedItem is int m ? m : DateTime.Today.Month;
        var count = DateTime.DaysInMonth(year, month);
        DayBox.ItemsSource = Enumerable.Range(1, count).ToList();
        DayBox.SelectedItem = Math.Clamp(preferred, 1, count);
    }

    private void OnDatePartChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        RepopulateDays(DayBox.SelectedItem is int day ? day : DateTime.Today.Day);
    }

    private void OnDailyChanged(object sender, RoutedEventArgs e) => UpdateDateEnabled();

    private void UpdateDateEnabled()
    {
        var daily = DailyCheck.IsChecked == true;
        YearBox.IsEnabled = !daily;
        MonthBox.IsEnabled = !daily;
        DayBox.IsEnabled = !daily;
    }

    private void OnEndChanged(object sender, RoutedEventArgs e) => UpdateEndEnabled();

    private void UpdateEndEnabled()
    {
        var enabled = EndCheck.IsChecked == true;
        EndHourBox.IsEnabled = enabled;
        EndMinuteBox.IsEnabled = enabled;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var year = YearBox.SelectedItem is int y ? y : DateTime.Today.Year;
        var month = MonthBox.SelectedItem is int m ? m : DateTime.Today.Month;
        var day = DayBox.SelectedItem is int d ? d : DateTime.Today.Day;
        var hour = HourBox.SelectedItem is int h ? h : 9;
        var minute = MinuteBox.SelectedItem is int n ? n : 0;
        var endHour = EndHourBox.SelectedItem is int eh ? eh : 10;
        var endMinute = EndMinuteBox.SelectedItem is int em ? em : 0;

        SelectedDate = DailyCheck.IsChecked == true ? "" : $"{year:0000}-{month:00}-{day:00}";
        SelectedStart = $"{hour:00}:{minute:00}";
        SelectedEnd = EndCheck.IsChecked == true ? $"{endHour:00}:{endMinute:00}" : "";
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
