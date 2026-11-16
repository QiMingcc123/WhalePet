using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WhalePet;

public partial class ColorPickerWindow : Window
{
    private static readonly string[] Palette =
    {
        "#4D6BFE", "#2F80ED", "#56CCF2", "#2D9CDB", "#00B8D9", "#36B37E", "#27AE60", "#6FCF97",
        "#F2C94C", "#F2994A", "#EB5757", "#C0392B", "#BB6BD9", "#9B51E0", "#E84393", "#D63384",
        "#EAF6FF", "#CFEAFF", "#9AB6CE", "#6F8CA6", "#3A6C95", "#1F4C72", "#0B1B3A", "#081A32",
        "#000000", "#333333", "#555555", "#777777", "#AAAAAA", "#CCCCCC", "#EEEEEE", "#FFFFFF",
    };

    private string _color;

    public string SelectedColor => _color;

    public ColorPickerWindow(string initial)
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
        _color = Normalize(initial) ?? "#4D6BFE";
        HexBox.Text = _color;
        BuildPalette();
        UpdatePreview();
    }

    private void BuildPalette()
    {
        foreach (var hex in Palette)
        {
            var swatch = new Button
            {
                Width = 36,
                Height = 28,
                Margin = new Thickness(3),
                Background = Brush(hex),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = hex,
                ToolTip = hex,
            };
            swatch.Click += (_, _) =>
            {
                _color = hex;
                HexBox.Text = hex;
                ErrorText.Text = "";
                UpdatePreview();
            };
            PaletteGrid.Children.Add(swatch);
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    private static string? Normalize(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) return null;
        if (!text.StartsWith('#')) text = "#" + text;
        return Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$") ? text.ToUpperInvariant() : null;
    }

    private void OnHexChanged(object sender, TextChangedEventArgs e)
    {
        var normalized = Normalize(HexBox.Text);
        ErrorText.Text = normalized is null ? "请输入 #RRGGBB，例如 #4D6BFE。" : "";
        if (normalized is not null)
        {
            _color = normalized;
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        try
        {
            Preview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_color));
        }
        catch
        {
            // Keep the previous preview.
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var normalized = Normalize(HexBox.Text);
        if (normalized is null)
        {
            ErrorText.Text = "颜色格式不正确，请输入 #RRGGBB。";
            return;
        }
        _color = normalized;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
