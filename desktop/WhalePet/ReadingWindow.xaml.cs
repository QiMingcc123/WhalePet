using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WhalePet.Game;

namespace WhalePet;

public partial class ReadingWindow : Window
{
    private PetSettings _settings;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _topmostTimer;
    private bool _loaded;
    private bool _applying;
    private int _page = 1;

    public event Action<PetSettings>? SettingsChanged;

    public ReadingWindow(PetSettings settings)
    {
        _settings = settings.Clone();
        InitializeComponent();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            PersistBounds();
        };
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _topmostTimer.Tick += (_, _) =>
        {
            _topmostTimer.Stop();
            if (IsVisible) PanelPlacementHelper.BringToTop(this);
        };
        ApplySettings(settings);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        ApplySettings(_settings);
        ReloadText();
        Activate();
        Focus();
        PanelPlacementHelper.BringToTop(this);
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        PanelPlacementHelper.BringToTop(this);
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _topmostTimer.Stop();
        if (IsVisible) _topmostTimer.Start();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        PanelPlacementHelper.AttachDisplayWatcher(this, PersistBounds, includeTaskbar: true);
    }

    public void ApplySettings(PetSettings settings)
    {
        _applying = true;
        try
        {
            var reload = !string.Equals(_settings.ReadingTextPath, settings.ReadingTextPath,
                             StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(_settings.ReadingEncoding, settings.ReadingEncoding,
                             StringComparison.Ordinal);
            _settings = settings.Clone();
            Width = Math.Clamp(settings.ReadingWidth, 30, 1200);
            Height = Math.Clamp(settings.ReadingHeight, 30, 800);
            if (_loaded)
            {
                PanelPlacementHelper.RestorePosition(this, settings.ReadingLeft, settings.ReadingTop);
            }

            var background = ParseColor(settings.ReadingBackground, Color.FromRgb(0x08, 0x1A, 0x32));
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(Math.Clamp(settings.ReadingBackgroundOpacity, 0, 1) * 255),
                background.R, background.G, background.B));
            var textColor = ParseColor(settings.ReadingTextColor, Color.FromRgb(0xEA, 0xF6, 0xFF));
            BodyText.Foreground = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(Math.Clamp(settings.ReadingTextOpacity, 0, 1) * 255),
                textColor.R, textColor.G, textColor.B));
            BodyText.FontSize = Math.Clamp(settings.ReadingFontSize, 8, 40);
            BodyText.FontFamily = new FontFamily(string.IsNullOrWhiteSpace(settings.ReadingFontFamily)
                ? "Microsoft YaHei UI"
                : settings.ReadingFontFamily);
            BodyText.LineHeight = Math.Clamp(settings.ReadingLineHeight, 1.0, 2.0) *
                                  Math.Max(8, settings.ReadingFontSize);
            BodyText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            if (reload || !_loaded)
            {
                ReloadText();
            }
            else
            {
                _page = Math.Clamp(settings.ReadingPageIndex, 1, ReadingService.PageCount);
                ShowPage();
            }
        }
        finally
        {
            _applying = false;
        }
    }

    public bool ReloadText()
    {
        var ok = ReadingService.Reload(_settings);
        _page = Math.Clamp(_settings.ReadingPageIndex, 1, ReadingService.PageCount);
        _settings.ReadingPageIndex = _page;
        EncodingWarning.Visibility = ReadingService.EncodingSuspect
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (ok) ShowPage();
        else BodyText.Text = ReadingService.LastError;
        return ok;
    }

    private void ShowPage()
    {
        if (ReadingService.PageCount <= 0)
        {
            BodyText.Text = ReadingService.LastError;
            return;
        }
        _page = Math.Clamp(_page, 1, ReadingService.PageCount);
        BodyText.Text = ReadingService.PageAt(_page);
    }

    public bool PreviousPage()
    {
        if (_page <= 1) return false;
        SetPage(_page - 1);
        return true;
    }

    public bool NextPage()
    {
        if (_page >= ReadingService.PageCount) return false;
        SetPage(_page + 1);
        return true;
    }

    private void SetPage(int page)
    {
        _page = Math.Clamp(page, 1, ReadingService.PageCount);
        _settings.ReadingPageIndex = _page;
        ShowPage();
        if (_loaded && !_applying) SettingsChanged?.Invoke(_settings.Clone());
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Matches(_settings.ReadingPrevHotkey, key))
        {
            e.Handled = PreviousPage();
        }
        else if (Matches(_settings.ReadingNextHotkey, key))
        {
            e.Handled = NextPage();
        }
    }

    private static bool Matches(string? text, Key key)
    {
        return HotkeyGesture.TryParse(text, out var gesture) && gesture.Matches(key, Keyboard.Modifiers);
    }

    private void OnBoundsChanged(object? sender, EventArgs e)
    {
        if (!_loaded || _applying) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void PersistBounds()
    {
        if (!_loaded) return;
        _settings.ReadingLeft = Left;
        _settings.ReadingTop = Top;
        _settings.ReadingWidth = Width;
        _settings.ReadingHeight = Height;
        if (!_applying) SettingsChanged?.Invoke(_settings.Clone());
    }

    public (double? Left, double? Top) ResetToPrimaryWorkArea()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width)
        {
            Left = area.Left;
        }
        else
        {
            Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        }
        if (Height > area.Height)
        {
            Top = area.Top;
        }
        else
        {
            Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
        }
        PanelPlacementHelper.BringToTop(this);
        PersistBounds();
        return (Left, Top);
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        OnDrag(sender, e);
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (WindowDragHelper.IsInteractive(e.OriginalSource)) return;
        Activate();
        Focus();
        var width = Width;
        var height = Height;
        try { DragMove(); } catch { }
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        Width = width;
        Height = height;
        PanelPlacementHelper.SnapToEdge(this, 18, includeTaskbar: true);
        PanelPlacementHelper.BringToTop(this);
        PersistBounds();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _saveTimer.Stop();
        PersistBounds();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        _topmostTimer.Stop();
    }

    private static Color ParseColor(string? text, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString((text ?? "").Trim());
        }
        catch
        {
            return fallback;
        }
    }
}
