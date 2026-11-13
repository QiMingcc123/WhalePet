using System.Windows;
using System.Windows.Media;

namespace WhalePet;

public partial class WorkBadgeWindow : Window
{
    public WorkBadgeWindow(PetSettings settings)
    {
        InitializeComponent();
        var scale = Math.Clamp(settings.WorkBadgeScale, 0.5, 2);
        BadgeScale.ScaleX = scale;
        BadgeScale.ScaleY = scale;
        BadgeText.FontSize = settings.WorkBadgeFontSize;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(settings.ThemeColor);
            Badge.BorderBrush = new SolidColorBrush(color);
        }
        catch
        {
            // Keep the default border.
        }
    }

    public void ShowBadge(string text)
    {
        BadgeText.Text = string.IsNullOrWhiteSpace(text) ? "搬砖中..." : text;
        if (!IsVisible) Show();
        UpdateLayout();
    }

    public void HideBadge()
    {
        if (IsVisible) Hide();
    }
}
