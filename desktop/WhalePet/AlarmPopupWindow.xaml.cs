using System.Windows;
using System.Windows.Media;

namespace WhalePet;

public partial class AlarmPopupWindow : Window
{
    public AlarmPopupWindow(string message, string time, string themeColor,
        string title = "闹钟提醒", string icon = "⏰")
    {
        InitializeComponent();
        MessageText.Text = message;
        TimeText.Text = time;
        TitleText.Text = title;
        IconText.Text = icon;
        Title = title;

        var accent = ThemePalette.Parse(themeColor, Color.FromRgb(0x4D, 0x6B, 0xFE));
        Card.BorderBrush = ThemePalette.Brush(accent);
        Card.Background = ThemePalette.Panel(themeColor, 0.98, 0.20);
        CloseButton.Background = ThemePalette.Brush(accent);

        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + Math.Max(0, (area.Width - ActualWidth) / 2);
            Top = area.Top + Math.Max(0, area.Height * 0.22);
        };
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
