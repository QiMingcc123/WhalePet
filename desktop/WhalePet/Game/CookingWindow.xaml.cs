using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WhalePet.Game;

/// <summary>
/// Cooking overlay anchored above the pet. The pet plays the cooking GIF while
/// the 10-second bar and three random cooking points are driven here.
/// </summary>
public partial class CookingWindow : Window
{
    private enum State
    {
        Setup,
        Cooking,
        Finished,
    }

    private sealed record Difficulty(string Label, string Key, int Required);

    private sealed class Zone
    {
        public double Start { get; init; }
        public Rectangle Shape { get; init; } = null!;
        public bool Hit { get; set; }
    }

    private static readonly Difficulty[] Difficulties =
    {
        new("简单", "easy", 1),
        new("普通", "normal", 2),
        new("困难", "hard", 3),
    };

    private const double TrackWidth = 460;
    private const double ZoneWidth = 3;

    private readonly GameEngine _engine;
    private readonly MainWindow _owner;
    private readonly List<Zone> _zones = new();
    private readonly DispatcherTimer _frameTimer;
    private readonly Stopwatch _watch = new();
    private DispatcherTimer? _closeTimer;
    private State _state = State.Setup;
    private Difficulty _difficulty = Difficulties[0];
    private int _hits;
    private bool _allowClose;

    public CookingWindow(GameEngine engine, MainWindow owner, PetSettings settings)
    {
        _engine = engine;
        _owner = owner;
        InitializeComponent();
        _frameTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
        _frameTimer.Tick += OnFrameTick;
        _owner.LocationChanged += OnOwnerMoved;
        _owner.SizeChanged += OnOwnerMoved;
        SizeChanged += OnOwnerMoved;
        RemainingText.Text = $"今日剩余 {_engine.CookingRemaining}/3 次";
        ApplyDisplaySettings(settings.CookingOverlayScale, settings.MiniGameOpacity);
    }

    public void ApplyDisplaySettings(double scale, double opacity)
    {
        var value = Math.Clamp(scale, 0.5, 2);
        OverlayScale.ScaleX = value;
        OverlayScale.ScaleY = value;
        Opacity = Math.Clamp(opacity, 0, 1);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Reposition));
    }

    private void FocusGame()
    {
        try
        {
            Activate();
            Focus();
            Keyboard.Focus(this);
            FocusManager.SetFocusedElement(this, this);
        }
        catch
        {
            // Focus is best effort.
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Reposition();
        FocusGame();
    }

    private void OnOwnerMoved(object? sender, EventArgs e) => Reposition();

    private void Reposition()
    {
        try
        {
            var bounds = _owner.CharacterBoundsInWindow();
            Left = _owner.Left + bounds.Left + (bounds.Width - ActualWidth) / 2;
            Top = _owner.Top + bounds.Top - ActualHeight - 8;
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(Left, area.Left + 4, Math.Max(area.Left + 4, area.Right - ActualWidth - 4));
            Top = Math.Clamp(Top, area.Top + 4, Math.Max(area.Top + 4, area.Bottom - ActualHeight - 4));
        }
        catch
        {
            // Best effort while the owner is laid out.
        }
    }

    private void OnDifficultyClick(object sender, RoutedEventArgs e)
    {
        if (_state != State.Setup || sender is not FrameworkElement { Tag: string key }) return;
        var difficulty = Difficulties.FirstOrDefault(item => item.Key == key);
        if (difficulty is null) return;
        if (!_engine.TryStartCooking())
        {
            SetupHint.Text = "没有食材或今日次数已经用完了。";
            return;
        }

        _difficulty = difficulty;
        _allowClose = false;
        SetupCard.Visibility = Visibility.Collapsed;
        GamePanel.Visibility = Visibility.Visible;
        _state = State.Cooking;
        _owner.SetTemporaryAvatar(GifCatalog.CookingGifId);
        _hits = 0;
        ResultText.Text = "";
        StatusText.Text = $"烹饪中（{difficulty.Label}）· 需要成功 {difficulty.Required} 次";
        HitText.Text = $"已成功 0/{difficulty.Required}";
        RemainingText.Text = $"今日剩余 {_engine.CookingRemaining}/3 次";
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Reposition();
            FocusGame();
        }));

        GenerateZones();
        Canvas.SetLeft(PointerBar, 0);
        _watch.Restart();
        _frameTimer.Start();
    }

    private void GenerateZones()
    {
        ZoneCanvas.Children.Clear();
        _zones.Clear();
        foreach (var start in PickStarts())
        {
            var shape = new Rectangle
            {
                Width = TrackWidth * ZoneWidth / 100.0,
                Height = 40,
                Fill = new SolidColorBrush(Color.FromArgb(0x99, 0x58, 0xE0, 0xB4)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x58, 0xE0, 0xB4)),
                StrokeThickness = 1,
                RadiusX = 3,
                RadiusY = 3,
            };
            Canvas.SetLeft(shape, TrackWidth * start / 100.0);
            Canvas.SetTop(shape, 0);
            ZoneCanvas.Children.Add(shape);
            _zones.Add(new Zone { Start = start, Shape = shape });
        }
    }

    private static List<double> PickStarts()
    {
        // Three 3%-wide points, after 20% and before 100%, never touching.
        for (var attempt = 0; attempt < 2000; attempt++)
        {
            var values = new List<double>();
            for (var i = 0; i < 3; i++) values.Add(21 + Random.Shared.NextDouble() * 75);
            values.Sort();
            if (values[1] - values[0] >= ZoneWidth + 1 &&
                values[2] - values[1] >= ZoneWidth + 1 &&
                values[2] + ZoneWidth <= 100)
            {
                return values;
            }
        }
        return new List<double> { 30, 55, 80 };
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        if (_state != State.Cooking) return;
        var progress = Math.Clamp(_watch.Elapsed.TotalSeconds / 10.0, 0, 1);
        Canvas.SetLeft(PointerBar, Math.Clamp(progress * TrackWidth - 1, 0, TrackWidth - 3));
        if (progress >= 1)
        {
            if (_hits >= _difficulty.Required)
            {
                Finish(true, $"烹饪成功！成功烹饪 {_hits} 次，鱼肉 +1。");
            }
            else
            {
                Finish(false, "火候没掌握好，烹饪失败");
            }
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Space) return;
        e.Handled = true;
        if (_state != State.Cooking) return;
        var progress = Math.Clamp(_watch.Elapsed.TotalSeconds / 10.0, 0, 1) * 100;
        if (progress >= 100) return;
        var zone = _zones.FirstOrDefault(item =>
            progress >= item.Start && progress <= item.Start + ZoneWidth);
        if (zone is null)
        {
            Finish(false, "火候没掌握好，烹饪失败");
            return;
        }
        if (zone.Hit) return;
        zone.Hit = true;
        zone.Shape.Fill = new SolidColorBrush(Color.FromRgb(0x9C, 0xFF, 0xDA));
        _hits++;
        HitText.Text = $"已成功 {_hits}/{_difficulty.Required}";
        if (_hits >= _difficulty.Required)
        {
            Finish(true, $"烹饪成功！成功烹饪 {_hits} 次，鱼肉 +1。");
        }
    }

    private void Finish(bool success, string message)
    {
        if (_state == State.Finished) return;
        _state = State.Finished;
        _frameTimer.Stop();
        _watch.Stop();
        if (success) _engine.FinishCooking(true, _difficulty.Key);
        _owner.ShowGameMessage(message);
        _owner.RestoreTemporaryAvatar();
        StatusText.Text = success ? "烹饪成功" : "烹饪失败";
        ResultText.Text = message;
        HitText.Text = $"已成功 {_hits}/{_difficulty.Required}";
        RemainingText.Text = $"今日剩余 {_engine.CookingRemaining}/3 次";
        _allowClose = true;
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer?.Stop();
            _closeTimer = null;
            Close();
        };
        _closeTimer.Start();
    }

    private void OnExitSetupClick(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose) e.Cancel = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        _closeTimer?.Stop();
        _owner.LocationChanged -= OnOwnerMoved;
        _owner.SizeChanged -= OnOwnerMoved;
        SizeChanged -= OnOwnerMoved;
        _owner.RestoreTemporaryAvatar();
        _engine.Save();
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }
}
