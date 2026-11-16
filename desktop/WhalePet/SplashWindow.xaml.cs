using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WhalePet;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();

        var settings = SettingsStore.Load();
        var accent = ThemePalette.Parse(settings.ThemeColor, Color.FromRgb(0x4D, 0x6B, 0xFE));
        Card.Background = ThemePalette.Panel(settings.ThemeColor, 0.98, 0.28);
        Card.BorderBrush = ThemePalette.Brush(accent, 0.75);
        AuthorText.Foreground = ThemePalette.Brush(ThemePalette.Lighten(accent, 0.62));
        Underline.Background = ThemePalette.Brush(accent);
    }

    public async Task ShowAsync()
    {
        Show();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)));
        CardScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(650)) { EasingFunction = ease });
        CardScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(650)) { EasingFunction = ease });
        AuthorPanel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700))
            {
                BeginTime = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease,
            });
        AuthorTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(700))
            {
                BeginTime = TimeSpan.FromMilliseconds(220),
                EasingFunction = ease,
            });
        Underline.BeginAnimation(
            WidthProperty,
            new DoubleAnimation(0, 210, TimeSpan.FromMilliseconds(750))
            {
                BeginTime = TimeSpan.FromMilliseconds(320),
                EasingFunction = ease,
            });

        await Task.Delay(2750);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(420));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
        await Task.Delay(430);
    }
}
