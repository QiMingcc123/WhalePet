using System.Windows;
using System.Windows.Media;

namespace WhalePet;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string message, string title, string confirmText, string themeColor)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        var accent = ThemePalette.Parse(themeColor, Color.FromRgb(0x4D, 0x6B, 0xFE));
        Card.BorderBrush = ThemePalette.Brush(accent);
        Card.Background = ThemePalette.Panel(themeColor, 0.98, 0.20);
        ConfirmButton.Background = ThemePalette.Brush(accent);
        ConfirmButton.BorderBrush = ThemePalette.Brush(accent);
        ConfirmButton.Foreground = Brushes.White;
        Loaded += (_, _) => CancelButton.Focus();
    }

    public static bool Show(Window? owner, string message, string title = "确认",
        string confirmText = "确定", string? themeColor = null)
    {
        var color = themeColor ?? SettingsStore.Load().ThemeColor;
        var dialog = new ConfirmDialog(message, title, confirmText, color);
        if (owner is not null) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
