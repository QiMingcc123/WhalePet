using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhalePet;

/// <summary>Small color helpers shared by the pet window, menus and dialogs.</summary>
public static class ThemePalette
{
    public static Color Parse(string? hex, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString((hex ?? "").Trim());
        }
        catch
        {
            return fallback;
        }
    }

    public static Color Blend(Color first, Color second, double firstWeight)
    {
        var weight = Math.Clamp(firstWeight, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(first.R * weight + second.R * (1 - weight)),
            (byte)Math.Round(first.G * weight + second.G * (1 - weight)),
            (byte)Math.Round(first.B * weight + second.B * (1 - weight)));
    }

    public static Color Darken(Color color, double amount) =>
        Blend(color, Colors.Black, 1 - Math.Clamp(amount, 0, 1));

    public static Color Lighten(Color color, double amount) =>
        Blend(color, Colors.White, 1 - Math.Clamp(amount, 0, 1));

    public static SolidColorBrush Brush(Color color, double opacity = 1)
    {
        var alpha = (byte)Math.Clamp(Math.Round(255 * opacity), 0, 255);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>A dark panel tinted by the theme color.</summary>
    public static SolidColorBrush Panel(string themeHex, double opacity = 0.96, double strength = 0.20)
    {
        var theme = Parse(themeHex, Color.FromRgb(0x4D, 0x6B, 0xFE));
        var mixed = Blend(theme, Color.FromRgb(0x06, 0x12, 0x26), strength);
        return Brush(mixed, opacity);
    }

    public static Color PanelColor(string themeHex, double strength = 0.20)
    {
        var theme = Parse(themeHex, Color.FromRgb(0x4D, 0x6B, 0xFE));
        return Blend(theme, Color.FromRgb(0x06, 0x12, 0x26), strength);
    }

    /// <summary>
    /// Background for a panel/window: the configured image (if enabled) with a
    /// dark readability tint, otherwise the theme-tinted dark panel.
    /// </summary>
    public static Brush Surface(string themeHex, bool useImage, string imagePath, double imageOpacity,
        double tintStrength = 0.18)
    {
        if (useImage && !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var bounds = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
                var group = new DrawingGroup();
                group.Children.Add(new ImageDrawing(bitmap, bounds));
                var tint = (byte)Math.Clamp(
                    Math.Round(255 * (1 - Math.Clamp(imageOpacity, 0, 1)) * 0.82), 0, 255);
                if (tint > 0)
                {
                    group.Children.Add(new GeometryDrawing(
                        new SolidColorBrush(Color.FromArgb(tint, 0x06, 0x12, 0x26)),
                        null,
                        new RectangleGeometry(bounds)));
                }

                var brush = new DrawingBrush(group)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                };
                brush.Freeze();
                return brush;
            }
            catch
            {
                // Fall back to the tinted panel.
            }
        }

        return Panel(themeHex, 1, tintStrength);
    }
}
