using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WhalePet.Game;

/// <summary>
/// Fishing overlay anchored above the pet. The pet itself plays the fishing
/// GIF while this window owns the ring, progress and timing rules.
/// </summary>
public partial class FishingWindow : Window
{
    private enum State
    {
        Setup,
        Waiting,
        Ring,
        Finished,
    }

    private sealed record Difficulty(string Label, string Key, double Share, double DurationSeconds);

    private static readonly Difficulty[] Difficulties =
    {
        new("简单", "easy", 0.25, 5),
        new("普通", "normal", 0.20, 8),
        new("困难", "hard", 0.15, 10),
        new("噩梦", "nightmare", 0.05, 15),
    };

    private readonly GameEngine _engine;
    private readonly MainWindow _owner;
    private readonly DispatcherTimer _frameTimer;
    private readonly Stopwatch _ringWatch = new();
    private DispatcherTimer? _biteTimer;
    private DispatcherTimer? _timeoutTimer;
    private DispatcherTimer? _closeTimer;
    private State _state = State.Setup;
    private Difficulty _difficulty = Difficulties[0];
    private double _zoneStart;
    private double _progress;
    private double _success;
    private double _lastPressAt;
    private double _lastFrameAt;
    private double _decayCarry;
    private bool _allowClose;

    public FishingWindow(GameEngine engine, MainWindow owner, PetSettings settings)
    {
        _engine = engine;
        _owner = owner;
        InitializeComponent();
        _frameTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        _frameTimer.Tick += OnFrameTick;
        _owner.LocationChanged += OnOwnerMoved;
        _owner.SizeChanged += OnOwnerMoved;
        SizeChanged += OnOwnerMoved;
        RemainingText.Text = $"今日剩余 {_engine.FishingRemaining}/5 次";
        ApplyDisplaySettings(settings.FishingOverlayScale, settings.MiniGameOpacity);
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
            // Focus is best effort; the global hook remains a fallback.
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
            // Repositioning is best effort while the owner is being laid out.
        }
    }

    private void OnDifficultyClick(object sender, RoutedEventArgs e)
    {
        if (_state != State.Setup || sender is not FrameworkElement { Tag: string key }) return;
        var difficulty = Difficulties.FirstOrDefault(item => item.Key == key);
        if (difficulty is null) return;
        if (!_engine.TrySpendFishing())
        {
            SetupHint.Text = "今日钓鱼次数已经用完了。";
            return;
        }

        _difficulty = difficulty;
        _allowClose = false;
        SetupCard.Visibility = Visibility.Collapsed;
        GamePanel.Visibility = Visibility.Visible;
        _state = State.Waiting;
        _owner.SetTemporaryAvatar(GifCatalog.FishingGifId);
        StatusText.Text = $"已抛竿（{difficulty.Label}），等待鱼儿上钩…";
        ResultText.Text = "";
        RemainingText.Text = $"今日剩余 {_engine.FishingRemaining}/5 次";
        HintText.Text = "鱼儿会在 10–20 秒内咬钩，这期间按空格没有反应。";
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Reposition();
            FocusGame();
        }));

        var delay = Random.Shared.Next(10, 21);
        _biteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        _biteTimer.Tick += OnBite;
        _biteTimer.Start();
    }

    private void OnBite(object? sender, EventArgs e)
    {
        _biteTimer?.Stop();
        _biteTimer = null;
        if (_state != State.Waiting) return;

        _state = State.Ring;
        _progress = 0;
        _success = 0;
        _decayCarry = 0;
        _lastPressAt = 0;
        _lastFrameAt = 0;
        var width = _difficulty.Share * 100;
        _zoneStart = 20 + Random.Shared.NextDouble() * Math.Max(0.01, 100 - width - 20);
        DrawSafeZone(_zoneStart, width);
        DrawSuccess(0);
        UpdateVisuals();

        RingCanvas.Visibility = Visibility.Visible;
        StatusText.Text = "咬钩了！按空格积累钓鱼进度";
        HintText.Text = "让橙黄色进度停在绿色成功区内，成功进度会持续增长；离开成功区会下降。";
        _ringWatch.Restart();
        _frameTimer.Start();
        _timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timeoutTimer.Tick += (_, _) => Finish(false, "鱼跑了……");
        _timeoutTimer.Start();
        FocusGame();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        if (_state != State.Ring) return;
        var now = _ringWatch.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastFrameAt, 0, 0.2);
        _lastFrameAt = now;

        if (now - _lastPressAt > 1.0)
        {
            _decayCarry += dt;
            while (_decayCarry >= 0.5)
            {
                _decayCarry -= 0.5;
                _progress = Math.Max(0, _progress - 2);
            }
        }
        else
        {
            _decayCarry = 0;
        }

        var width = _difficulty.Share * 100;
        var inZone = _progress >= _zoneStart && _progress <= _zoneStart + width;
        _success = Math.Clamp(
            _success + (inZone ? dt * 100.0 / _difficulty.DurationSeconds : -dt * 10.0),
            0, 100);
        UpdateVisuals();

        if (_success >= 100)
        {
            Finish(true, "收杆成功！小鱼 +1。");
            return;
        }
        if (now >= 60)
        {
            Finish(false, "鱼跑了……");
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        e.Handled = true;
        if (_state != State.Ring) return;

        _progress = Math.Min(100, _progress + 2);
        _lastPressAt = _ringWatch.Elapsed.TotalSeconds;
        _decayCarry = 0;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var angle = _progress * 3.6;
        var radians = angle * Math.PI / 180.0;
        const double center = 140;
        const double radius = 108;
        var x = center + radius * Math.Sin(radians);
        var y = center - radius * Math.Cos(radians);
        Canvas.SetLeft(ProgressMarker, x - ProgressMarker.Width / 2);
        Canvas.SetTop(ProgressMarker, y - ProgressMarker.Height / 2);
        ProgressMarker.RenderTransform = Transform.Identity;
        DrawSuccess(_success);
        var remain = Math.Max(0, 60 - _ringWatch.Elapsed.TotalSeconds);
        TimerText.Text = $"{remain:0}s";
    }

    private void DrawSafeZone(double startPercent, double widthPercent)
    {
        var start = startPercent * 3.6;
        var sweep = widthPercent * 3.6;
        SafeZonePath.Data = BuildAnnularSector(140, 140, 78, 108, start, sweep);
    }

    private void DrawSuccess(double percent)
    {
        var sweep = Math.Clamp(percent, 0, 100) * 3.6;
        SuccessArc.Data = BuildArc(140, 140, 60, 0, sweep);
    }

    private static Point OnCircle(double cx, double cy, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Point(cx + radius * Math.Sin(radians), cy - radius * Math.Cos(radians));
    }

    private static Geometry BuildArc(double cx, double cy, double radius, double start, double sweep)
    {
        if (sweep <= 0.1) return Geometry.Empty;
        if (sweep >= 359.9) return new EllipseGeometry(new Point(cx, cy), radius, radius);

        var figure = new PathFigure
        {
            StartPoint = OnCircle(cx, cy, radius, start),
            IsClosed = false,
        };
        figure.Segments.Add(new ArcSegment(
            OnCircle(cx, cy, radius, start + sweep),
            new Size(radius, radius),
            0,
            sweep > 180,
            SweepDirection.Clockwise,
            true));
        return new PathGeometry(new[] { figure });
    }

    private static Geometry BuildAnnularSector(
        double cx, double cy, double inner, double outer, double start, double sweep)
    {
        if (sweep <= 0.1) return Geometry.Empty;
        var outerStart = OnCircle(cx, cy, outer, start);
        var outerEnd = OnCircle(cx, cy, outer, start + sweep);
        var innerEnd = OnCircle(cx, cy, inner, start + sweep);
        var innerStart = OnCircle(cx, cy, inner, start);
        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
        figure.Segments.Add(new ArcSegment(outerEnd, new Size(outer, outer), 0, sweep > 180,
            SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new ArcSegment(innerStart, new Size(inner, inner), 0, sweep > 180,
            SweepDirection.Counterclockwise, true));
        figure.Segments.Add(new LineSegment(outerStart, true));
        return new PathGeometry(new[] { figure });
    }

    private void Finish(bool success, string message)
    {
        if (_state == State.Finished) return;
        _state = State.Finished;
        _frameTimer.Stop();
        _timeoutTimer?.Stop();
        _timeoutTimer = null;
        _biteTimer?.Stop();
        _biteTimer = null;

        if (success) _engine.FinishFishing(true, _difficulty.Key);
        _owner.ShowGameMessage(success ? "钓鱼成功，小鱼 +1。" : "鱼跑了……");
        _owner.RestoreTemporaryAvatar();

        StatusText.Text = success ? "钓鱼成功" : "钓鱼失败";
        ResultText.Text = message;
        HintText.Text = success ? "小鱼已放入背包食材区。" : "本次不返还次数，下次再试吧。";
        RemainingText.Text = $"今日剩余 {_engine.FishingRemaining}/5 次";
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
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        _biteTimer?.Stop();
        _timeoutTimer?.Stop();
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
