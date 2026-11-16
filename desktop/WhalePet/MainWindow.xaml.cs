using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WhalePet;

public partial class MainWindow : Window
{
    private enum PetState
    {
        Main,
        Idle,
        Play,
    }

    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _inputTimer;
    private readonly DispatcherTimer _deviceTimer;
    private readonly DispatcherTimer _clipboardTimer;
    private readonly DispatcherTimer _statusMarqueeTimer;
    private bool _statusScrollPaused;
    private int _statusScrollDirection = 1;
    private readonly WeatherService _weatherService = new();
    private readonly Random _random = new();
    private PetSettings _settings;
    private PetState _state = PetState.Main;
    private DateTime _playUntil;
    private string _currentImage = "";
    private bool _mouseDown;
    private bool _dragging;
    private double _dragCursorX;
    private double _dragCursorY;
    private double _dragStartLeft;
    private double _dragStartTop;
    private double _anchorBottom = double.NaN;
    private int _fadeToken;
    private int _bubbleToken;
    private DateTime _lastWeatherFetch = DateTime.MinValue;
    private WeatherInfo? _weatherInfo;
    private CancellationTokenSource? _weatherCts;
    private bool _detailsOpen;
    private bool _adjustingPosition;
    private bool _loaded;
    private bool _closing;
    private GlobalInputHook? _inputHook;
    private DateTime _typingUntil;
    private DateTime _pointerUntil;
    private DateTime _pressedUntil;
    private DateTime _lastInteraction = DateTime.Now;
    private DateTime _lastProactive = DateTime.MinValue;
    private DateTime _workReminderDate = DateTime.MinValue;
    private bool _beforeWorkReminderShown;
    private bool _endWorkReminderShown;
    private DateTime _nextWaterReminder = DateTime.MinValue;
    private DateTime _nextSedentary = DateTime.MinValue;
    private readonly Dictionary<int, DateTime> _alarmFiredDates = new();
    private readonly List<ClipboardEntry> _clipboardEntries = new();
    private string? _lastClipboardText;
    private bool _clipboardPrimed;
    private bool _clipboardPaused;
    private bool _removableBusy;
    private readonly List<DriveInfo> _removableDrives = new();
    private WorkBadgeWindow? _workBadgeWindow;
    private AlarmPopupWindow? _alarmPopupWindow;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _screenshotHotkeyRegistered;
    private const int ScreenshotHotkeyId = 0x5151;
    private bool _deviceInfoBusy;
    private EdgeSide _edgeSide = EdgeSide.None;
    private Rect _expandedBounds;
    private bool _collapsedAtEdge;
    private bool _suppressEdgeCollapse;
    private readonly DispatcherTimer _edgeLeaveTimer;
    private int _memoCount;

    private enum EdgeSide
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
    }

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        _settings = SettingsStore.Load();
        _settings.Normalize();
        _nextWaterReminder = DateTime.Now.AddMinutes(_settings.WaterReminderMinutes);
        _nextSedentary = DateTime.Now.AddMinutes(_settings.SedentaryMinutes > 0 ? _settings.SedentaryMinutes : 45);
        _clipboardEntries.AddRange(ClipboardStore.Load());
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();
        _inputTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _inputTimer.Tick += (_, _) => UpdateInputVisuals();
        _inputTimer.Start();
        _deviceTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
        _deviceTimer.Tick += (_, _) =>
        {
            RefreshDeviceInfo();
            UpdateRemovableDevices();
        };
        _deviceTimer.Start();
        _clipboardTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(900) };
        _clipboardTimer.Tick += (_, _) => PollClipboard();
        _clipboardTimer.Start();
        _statusMarqueeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        _statusMarqueeTimer.Tick += (_, _) => AdvanceStatusMarquee();
        _statusMarqueeTimer.Start();
        _edgeLeaveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(420) };
        _edgeLeaveTimer.Tick += OnEdgeLeaveTimer;
        ApplySettingsToUi();
        UpdateStatusText();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        AutoStartService.SyncSilently(_settings.StartWithWindows);
        UpdateMemoCount();
        _inputHook = new GlobalInputHook();
        _inputHook.KeyboardPressed += OnGlobalKeyboard;
        _inputHook.PointerMoved += OnGlobalPointerMoved;
        _inputHook.PointerPressed += OnGlobalPointerPressed;
        UpdateLayout();
        RestorePosition();
        SetState(PetState.Main, true);
        ApplyAppearance();
        PositionStatusActions();
        ShowGreeting();
        UpdateStatusText();
        UpdateWorkState();
        UpdateRemovableDevices();
        UpdateStatusActions();
        _nextWaterReminder = DateTime.Now.AddMinutes(_settings.WaterReminderMinutes);
        RefreshDeviceInfo();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyAppearance));
        _ = RefreshWeatherAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
        RegisterScreenshotHotkey();
    }

    private void RegisterScreenshotHotkey()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_screenshotHotkeyRegistered)
        {
            HotkeyManager.Unregister(_hwnd, ScreenshotHotkeyId);
            _screenshotHotkeyRegistered = false;
        }
        if (!_settings.ScreenshotEnabled) return;

        var modifiers = HotkeyManager.Modifiers(
            _settings.ScreenshotCtrl,
            _settings.ScreenshotAlt,
            _settings.ScreenshotShift,
            _settings.ScreenshotWin);
        var key = HotkeyManager.VirtualKey(_settings.ScreenshotKey);
        if (key == 0) return;
        if (HotkeyManager.Register(_hwnd, ScreenshotHotkeyId, modifiers, key))
        {
            _screenshotHotkeyRegistered = true;
        }
        else
        {
            ShowSpeech("截图快捷键注册失败，可能已被其他程序占用。");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyManager.WmHotkey && wParam.ToInt32() == ScreenshotHotkeyId)
        {
            handled = true;
            StartScreenshot();
        }
        return IntPtr.Zero;
    }

    private void StartScreenshot()
    {
        if (_closing || !_settings.ScreenshotEnabled) return;
        try
        {
            var overlay = ScreenshotOverlayWindow.Create();
            overlay.Show();
            overlay.Activate();
        }
        catch (Exception error)
        {
            ShowSpeech("截图失败：" + error.Message);
        }
    }

    private void OnScreenshotClick(object sender, RoutedEventArgs e) => StartScreenshot();

    private void OnGlobalKeyboard()
    {
        _typingUntil = DateTime.Now.AddMilliseconds(1200);
    }

    private void OnGlobalPointerMoved(int x, int y)
    {
        _pointerUntil = DateTime.Now.AddMilliseconds(1600);
        PositionPointerGlyph(x, y);
    }

    private void OnGlobalPointerPressed(int x, int y)
    {
        _pressedUntil = DateTime.Now.AddMilliseconds(260);
        _pointerUntil = DateTime.Now.AddMilliseconds(1600);
        PositionPointerGlyph(x, y);
        AnimatePress();
    }

    private void PositionPointerGlyph(int screenX, int screenY)
    {
        try
        {
            var point = CharacterHost.PointFromScreen(new Point(screenX, screenY));
            var visible = point.X >= 0 && point.Y >= 0 && point.X <= CharacterHost.ActualWidth && point.Y <= CharacterHost.ActualHeight;
            PointerGlyph.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible)
            {
                Canvas.SetLeft(PointerGlyph, point.X + 10);
                Canvas.SetTop(PointerGlyph, point.Y + 8);
            }
        }
        catch
        {
            PointerGlyph.Visibility = Visibility.Collapsed;
        }
    }

    private void AnimatePress()
    {
        var duration = TimeSpan.FromMilliseconds(90);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        CharacterPressScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 1.015, duration) { AutoReverse = true, EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        CharacterPressScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.975, duration) { AutoReverse = true, EasingFunction = ease, FillBehavior = FillBehavior.Stop });
    }

    private void UpdateInputVisuals()
    {
        if (_closing) return;
        if (_collapsedAtEdge)
        {
            var cursor = Native.GetCursorPosition();
            var point = PointFromScreen(new Point(cursor.X, cursor.Y));
            if (point.X >= 0 && point.X <= ActualWidth && point.Y >= 0 && point.Y <= ActualHeight)
            {
                ExpandFromEdge();
            }
            return;
        }
        var now = DateTime.Now;
        var typing = now < _typingUntil;
        var pointer = now < _pointerUntil;
        var pressed = now < _pressedUntil;

        if (pressed)
        {
            InputBadge.Visibility = Visibility.Visible;
            InputIcon.Text = "🖱";
            InputText.Text = "点击";
            KeycapPanel.Visibility = Visibility.Collapsed;
        }
        else if (typing)
        {
            InputBadge.Visibility = Visibility.Visible;
            InputIcon.Text = "⌨";
            InputText.Text = "键盘输入";
            KeycapPanel.Visibility = Visibility.Visible;
        }
        else if (pointer)
        {
            InputBadge.Visibility = Visibility.Visible;
            InputIcon.Text = "🖱";
            InputText.Text = "鼠标移动";
            KeycapPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            InputBadge.Visibility = Visibility.Collapsed;
            PointerGlyph.Visibility = Visibility.Collapsed;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var idle = Native.GetIdleTime();
        if (_state == PetState.Play)
        {
            if (DateTime.Now >= _playUntil)
            {
                _playUntil = default;
                SetState(idle.TotalMinutes >= _settings.IdleMinutes ? PetState.Idle : PetState.Main);
            }
        }
        else
        {
            EvaluateIdleState(idle);
        }

        if (DateTime.Now - _lastWeatherFetch > TimeSpan.FromMinutes(20))
        {
            _ = RefreshWeatherAsync();
        }

        UpdateWorkState();
        UpdateWorkReminders();

        var now = DateTime.Now;
        if (_settings.ShowWaterReminder && now >= _nextWaterReminder)
        {
            _nextWaterReminder = now.AddMinutes(_settings.WaterReminderMinutes);
            ShowSpeech("该喝水啦，起来活动一下~");
        }
        UpdateSedentaryReminder(idle, now);
        UpdateAlarms(now);
        if (_settings.ChatIdleMinutes > 0
            && _state != PetState.Idle
            && _state != PetState.Play
            && SpeechBubble.Visibility != Visibility.Visible
            && now - _lastInteraction >= TimeSpan.FromMinutes(_settings.ChatIdleMinutes)
            && now - _lastProactive >= TimeSpan.FromMinutes(_settings.ChatIdleMinutes))
        {
            var phrases = _settings.ChatPhrases;
            if (phrases.Count > 0)
            {
                _lastProactive = now;
                ShowSpeech(phrases[_random.Next(phrases.Count)]);
            }
        }

        UpdateStatusText();
    }

    private void UpdateSedentaryReminder(TimeSpan idle, DateTime now)
    {
        var minutes = _settings.SedentaryMinutes;
        if (!_settings.SedentaryReminderEnabled || minutes <= 0) return;
        if (now < _nextSedentary) return;

        // If the user has been away for a while, restart the sitting timer instead
        // of nagging an empty chair.
        if (idle.TotalMinutes >= 5)
        {
            _nextSedentary = now.AddMinutes(minutes);
            return;
        }
        if (SpeechBubble.Visibility == Visibility.Visible) return;

        _nextSedentary = now.AddMinutes(minutes);
        ShowSpeech($"已经坐了 {minutes} 分钟啦，起来活动一下肩颈吧～");
    }

    private void UpdateAlarms(DateTime now)
    {
        if (!_settings.AlarmsEnabled || _settings.Alarms.Count == 0) return;
        if (SpeechBubble.Visibility == Visibility.Visible) return;

        var minuteOfDay = now.Hour * 60 + now.Minute;
        for (var index = 0; index < _settings.Alarms.Count; index++)
        {
            var alarm = _settings.Alarms[index];
            if (!alarm.Enabled || !TryParseClock(alarm.Start, out var start)) continue;
            if (!alarm.IsDaily && !string.Equals(alarm.Date, now.ToString("yyyy-MM-dd"), StringComparison.Ordinal)) continue;

            var hasEnd = TryParseClock(alarm.End, out var end) && end >= start;
            if (minuteOfDay < start) continue;
            if (hasEnd ? minuteOfDay > end : minuteOfDay > start) continue;
            if (_alarmFiredDates.TryGetValue(index, out var fired) && fired.Date == now.Date) continue;

            _alarmFiredDates[index] = now;
            ShowSpeech($"⏰ {alarm.Text}");
            if (alarm.Popup) ShowAlarmPopup(alarm);
            break;
        }
    }

    private static bool TryParseClock(string? value, out int minutes)
    {
        minutes = 0;
        if (!TimeSpan.TryParseExact((value ?? "").Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }
        minutes = (int)parsed.TotalMinutes;
        return minutes is >= 0 and < 1440;
    }

    private void ShowAlarmPopup(AlarmItem alarm)
    {
        _alarmPopupWindow?.Close();
        var when = alarm.IsDaily ? $"每天 {alarm.Start}" : $"{alarm.Date} {alarm.Start}";
        _alarmPopupWindow = new AlarmPopupWindow(alarm.Text, when, _settings.ThemeColor) { Owner = this };
        _alarmPopupWindow.Closed += (_, _) => _alarmPopupWindow = null;
        _alarmPopupWindow.Show();
    }

    private void UpdateWorkState()
    {
        if (!_loaded)
        {
            _workBadgeWindow?.HideBadge();
            return;
        }
        var now = DateTime.Now;
        var workDay = _settings.WorkDays.Contains((int)now.DayOfWeek);
        var start = ParseWorkTime(_settings.WorkStart, TimeSpan.FromHours(9));
        var end = ParseWorkTime(_settings.WorkEnd, TimeSpan.FromHours(18));
        var inWork = workDay && now.TimeOfDay >= start && now.TimeOfDay < end;
        WorkBadge.Visibility = Visibility.Collapsed;
        var showBadge = _settings.ShowWorkBadge && inWork && !_collapsedAtEdge;
        if (showBadge)
        {
            if (_workBadgeWindow is null)
            {
                _workBadgeWindow = new WorkBadgeWindow(_settings);
                _workBadgeWindow.Owner = this;
            }
            _workBadgeWindow.ShowBadge(_settings.WorkBadgeText);
            PositionWorkBadge();
        }
        else
        {
            _workBadgeWindow?.HideBadge();
        }
    }

    private void PositionWorkBadge()
    {
        if (_workBadgeWindow is not { IsVisible: true } badge) return;
        var statusWidth = Math.Max(120, SummaryButton.ActualWidth);
        badge.MaxWidth = Math.Max(100, statusWidth - 12);
        badge.UpdateLayout();
        var area = SystemParameters.WorkArea;
        var width = badge.ActualWidth;
        var height = badge.ActualHeight;
        var statusPoint = SummaryButton.TranslatePoint(new Point(0, 0), this);
        var statusLeft = Left + statusPoint.X;
        var statusTop = Top + statusPoint.Y;
        // Sit just above the status bar, 12 px gap, and stay inside its horizontal span.
        var left = statusLeft + 6;
        if (left + width > area.Right) left = area.Right - width;
        if (left < area.Left) left = area.Left;
        var top = statusTop - height - 12;
        if (top < area.Top) top = area.Top;
        badge.Left = left;
        badge.Top = top;
    }

    private void UpdateWorkReminders()
    {
        var now = DateTime.Now;
        var workDay = _settings.WorkDays.Contains((int)now.DayOfWeek);
        var end = ParseWorkTime(_settings.WorkEnd, TimeSpan.FromHours(18));
        if (!workDay) return;
        if (_workReminderDate.Date != now.Date)
        {
            _workReminderDate = now.Date;
            _beforeWorkReminderShown = false;
            _endWorkReminderShown = false;
        }

        if (_settings.ShowBeforeWorkReminder && !_beforeWorkReminderShown
            && now.TimeOfDay >= end - TimeSpan.FromMinutes(5) && now.TimeOfDay < end)
        {
            _beforeWorkReminderShown = true;
            ShowSpeech(_settings.WorkReminderBeforeText);
        }
        if (_settings.ShowEndWorkReminder && !_endWorkReminderShown
            && now.TimeOfDay >= end && now.TimeOfDay < end + TimeSpan.FromMinutes(5))
        {
            _endWorkReminderShown = true;
            ShowSpeech(_settings.WorkReminderEndText);
        }
    }

    private static TimeSpan ParseWorkTime(string value, TimeSpan fallback)
    {
        return TimeSpan.TryParseExact(value, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private async void RefreshDeviceInfo()
    {
        if (_deviceInfoBusy || _closing) return;
        if (!_settings.ShowDeviceInfo)
        {
            DeviceInfoPanel.Visibility = Visibility.Collapsed;
            return;
        }
        _deviceInfoBusy = true;
        try
        {
            var snapshot = await Task.Run(DeviceInfoService.Capture);
            if (_closing) return;
            DeviceMemoryText.Text = snapshot.Memory;
            DeviceCpuText.Text = snapshot.Cpu;
            DeviceGpuText.Text = snapshot.Gpu;
            DeviceDiskText.Text = snapshot.Disk;
            DeviceNetworkText.Text = snapshot.Network;
            DeviceMemoryText.Visibility = string.IsNullOrWhiteSpace(snapshot.Memory) ? Visibility.Collapsed : Visibility.Visible;
            DeviceCpuText.Visibility = string.IsNullOrWhiteSpace(snapshot.Cpu) ? Visibility.Collapsed : Visibility.Visible;
            DeviceGpuText.Visibility = string.IsNullOrWhiteSpace(snapshot.Gpu) ? Visibility.Collapsed : Visibility.Visible;
            DeviceDiskText.Visibility = string.IsNullOrWhiteSpace(snapshot.Disk) ? Visibility.Collapsed : Visibility.Visible;
            DeviceNetworkText.Visibility = string.IsNullOrWhiteSpace(snapshot.Network) ? Visibility.Collapsed : Visibility.Visible;
            var any = !string.IsNullOrWhiteSpace(snapshot.Memory)
                || !string.IsNullOrWhiteSpace(snapshot.Cpu)
                || !string.IsNullOrWhiteSpace(snapshot.Gpu)
                || !string.IsNullOrWhiteSpace(snapshot.Disk)
                || !string.IsNullOrWhiteSpace(snapshot.Network);
            DeviceInfoPanel.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            DeviceInfoPanel.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _deviceInfoBusy = false;
        }
    }

    private async void UpdateRemovableDevices()
    {
        if (_removableBusy) return;
        _removableBusy = true;
        try
        {
            var letters = await Task.Run(RemovableDeviceService.FindDriveLetters);
            var drives = new List<DriveInfo>();
            foreach (var letter in letters)
            {
                try
                {
                    var drive = new DriveInfo(letter);
                    if (drive.IsReady) drives.Add(drive);
                }
                catch
                {
                    // A drive can disappear while enumerating.
                }
            }

            _removableDrives.Clear();
            _removableDrives.AddRange(drives);
            if (_removableDrives.Count == 0)
            {
                UsbButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                var first = _removableDrives[0];
                UsbButton.ToolTip = $"移动设备：{first.Name.TrimEnd('\\')} · {FormatDriveSpace(first)}";
                UsbButton.Visibility = _settings.ShowRemovableDevices ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateStatusActions();
        }
        finally
        {
            _removableBusy = false;
        }
    }

    private static string FormatDriveSpace(DriveInfo drive)
    {
        try
        {
            var total = drive.TotalSize / 1024d / 1024d / 1024d;
            var free = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            return $"可用 {free:0.0} / {total:0.0} GB";
        }
        catch
        {
            return "设备信息不可用";
        }
    }

    private void UpdateStatusActions()
    {
        UsbButton.Visibility = _settings.ShowRemovableDevices && _removableDrives.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LockStatusButton.Visibility = _settings.ShowStatusBar && _settings.LockScreenEnabled ? Visibility.Visible : Visibility.Collapsed;
        StatusActions.Visibility = _settings.ShowStatusBar
            && (UsbButton.Visibility == Visibility.Visible || LockStatusButton.Visibility == Visibility.Visible)
            ? Visibility.Visible
            : Visibility.Collapsed;
        PositionStatusActions();
    }

    private void PositionStatusActions()
    {
        if (StatusActions.Visibility != Visibility.Visible) return;
        if (SummaryButton.ActualWidth <= 0 || Root.ActualWidth <= 0) return;

        Root.UpdateLayout();
        var actionsWidth = StatusActions.ActualWidth;
        if (actionsWidth <= 0)
        {
            StatusActions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            actionsWidth = StatusActions.DesiredSize.Width;
        }

        var statusWidth = SummaryButton.ActualWidth;
        var statusLeft = SummaryButton.TranslatePoint(new Point(0, 0), Root).X;
        var rootWidth = Root.ActualWidth;
        var inside = statusWidth + actionsWidth + 8 > rootWidth;
        var left = inside ? statusLeft + statusWidth - actionsWidth - 4 : statusLeft + statusWidth + 4;
        StatusActions.Margin = new Thickness(Math.Max(0, left), -26, 0, 0);
    }

    private void OnStatusWheel(object sender, MouseWheelEventArgs e)
    {
        if (StatusTextScroll.ScrollableWidth <= 0) return;
        var offset = Math.Clamp(StatusTextScroll.HorizontalOffset - e.Delta, 0, StatusTextScroll.ScrollableWidth);
        StatusTextScroll.ScrollToHorizontalOffset(offset);
        e.Handled = true;
    }

    private void AdvanceStatusMarquee()
    {
        if (_closing || !_settings.ShowStatusBar || _statusScrollPaused) return;
        var overflow = StatusTextScroll.ScrollableWidth;
        if (overflow <= 1) return;

        var offset = StatusTextScroll.HorizontalOffset + _statusScrollDirection * 1.4;
        if (offset >= overflow)
        {
            offset = overflow;
            _statusScrollDirection = -1;
        }
        else if (offset <= 0)
        {
            offset = 0;
            _statusScrollDirection = 1;
        }
        StatusTextScroll.ScrollToHorizontalOffset(offset);
    }

    private void OnStatusTextMouseEnter(object sender, MouseEventArgs e)
    {
        _statusScrollPaused = true;
        if (StatusTextScroll.ScrollableWidth > 1)
        {
            StatusTextScroll.ToolTip = "已暂停滚动，鼠标移开后继续";
        }
    }

    private void OnStatusTextMouseLeave(object sender, MouseEventArgs e)
    {
        _statusScrollPaused = false;
        if (StatusTextScroll.ScrollableWidth > 1)
        {
            StatusTextScroll.ToolTip = "鼠标悬停暂停滚动";
        }
    }

    private void RefreshStatusMarquee()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_closing) return;
            var scrollable = StatusTextScroll.ScrollableWidth > 1;
            StatusTextScroll.Cursor = scrollable ? Cursors.Hand : Cursors.Arrow;
            StatusTextScroll.ToolTip = scrollable
                ? (_statusScrollPaused ? "已暂停滚动，鼠标移开后继续" : "鼠标悬停暂停滚动")
                : null;
            if (!scrollable)
            {
                _statusScrollPaused = false;
                StatusTextScroll.ScrollToHorizontalOffset(0);
            }
        }));
    }

    private void OnUsbClick(object sender, RoutedEventArgs e)
    {
        if (_removableDrives.Count == 0) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", _removableDrives[0].RootDirectory.FullName) { UseShellExecute = true });
        }
        catch
        {
            // Ignore Explorer failures.
        }
    }

    private void OnUsbRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_removableDrives.Count == 0) return;
        var menu = new ContextMenu();
        foreach (var drive in _removableDrives)
        {
            var item = new MenuItem { Header = $"安全弹出 {drive.Name.TrimEnd('\\')}" };
            var root = drive.RootDirectory.FullName.TrimEnd('\\');
            item.Click += (_, _) => _ = EjectDriveAsync(root);
            menu.Items.Add(item);
        }
        menu.PlacementTarget = UsbButton;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private async Task EjectDriveAsync(string root)
    {
        var label = root.TrimEnd('\\');
        ShowSpeech($"正在安全弹出 {label}，请稍候…");

        EjectResult result;
        try
        {
            result = await Task.Run(() => DeviceEjector.Eject(root));
        }
        catch (Exception error)
        {
            result = new EjectResult(false, $"弹出 {label} 失败：{error.Message}");
        }

        if (_closing) return;
        ShowSpeech(result.Message);
        UpdateRemovableDevices();
    }

    private void EvaluateIdleState(TimeSpan idle)
    {
        if (idle.TotalMinutes >= _settings.IdleMinutes)
        {
            if (_state != PetState.Idle) SetState(PetState.Idle);
        }
        else if (_state == PetState.Idle)
        {
            SetState(PetState.Main);
        }
    }

    private void SetState(PetState state, bool force = false)
    {
        if (!force && _state == state) return;
        _state = state;
        var list = state == PetState.Idle
            ? _settings.IdleImages
            : state == PetState.Play
                ? _settings.PlayImages
                : _settings.MainImages;
        var selected = PickImage(list);
        if (selected.Length > 0) ShowImage(selected);
        StatusDot.Fill = state == PetState.Idle
            ? new SolidColorBrush(Color.FromRgb(124, 147, 170))
            : new SolidColorBrush(Color.FromRgb(88, 224, 180));
        UpdateStatusText();
    }

    private string PickImage(List<string> images)
    {
        var available = images
            .Select(_settings.ResolveImagePath)
            .Where(File.Exists)
            .ToList();
        if (available.Count == 0)
        {
            var defaults = PetSettings.CreateDefault();
            var fallback = ReferenceEquals(images, _settings.IdleImages)
                ? defaults.IdleImages
                : ReferenceEquals(images, _settings.PlayImages)
                    ? defaults.PlayImages
                    : defaults.MainImages;
            available = fallback
                .Select(_settings.ResolveImagePath)
                .Where(File.Exists)
                .ToList();
            if (available.Count == 0) return "";
        }

        if (available.Count == 1) return available[0];
        var choices = available.Where(path => !string.Equals(path, _currentImage, StringComparison.OrdinalIgnoreCase)).ToList();
        var pool = choices.Count > 0 ? choices : available;
        return pool[_random.Next(pool.Count)];
    }

    private void ShowImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        BitmapImage bitmap;
        try
        {
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth = 640;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
        }
        catch
        {
            return;
        }

        if (FrontImage.Source is null)
        {
            FrontImage.Source = bitmap;
            FrontImage.Opacity = 1;
            _currentImage = path;
            return;
        }

        var token = ++_fadeToken;
        var duration = TimeSpan.FromMilliseconds(Math.Clamp(_settings.TransitionMs, 120, 2000));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        FrontImage.BeginAnimation(OpacityProperty, null);
        BackImage.BeginAnimation(OpacityProperty, null);
        FrontImage.Opacity = 1;
        BackImage.Opacity = 0;
        BackImage.Source = bitmap;

        var fadeIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        fadeIn.Completed += (_, _) =>
        {
            if (token != _fadeToken) return;
            FrontImage.BeginAnimation(OpacityProperty, null);
            BackImage.BeginAnimation(OpacityProperty, null);
            FrontImage.Source = bitmap;
            FrontImage.Opacity = 1;
            BackImage.Source = null;
            BackImage.Opacity = 0;
            _currentImage = path;
        };
        var fadeOut = new DoubleAnimation(1, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        BackImage.BeginAnimation(OpacityProperty, fadeIn);
        FrontImage.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ShowGreeting()
    {
        if (!_settings.GreetingEnabled) return;
        ShowSpeech(GreetingForHour(DateTime.Now.Hour), true);
    }

    private static string GreetingForHour(int hour) => hour switch
    {
        < 5 => "夜深了，鲸鱼娘陪你守一会儿。",
        < 9 => "早上好，今天想先做哪件事？",
        < 12 => "上午好，慢慢来，海会等你。",
        < 14 => "中午好，记得休息一下眼睛。",
        < 18 => "下午好，继续写点漂亮的东西吧。",
        < 23 => "晚上好，今天也辛苦了。",
        _ => "夜深了，别熬太久哦。",
    };

    private static string Shorten(string text, int maxLength)
    {
        var value = (text ?? "").Trim();
        return value.Length > maxLength ? value[..(maxLength - 1)] + "…" : value;
    }

    private void ShowSpeech(string text, bool greeting = false)
    {
        SpeechText.Text = Shorten(text, 100);
        SpeechText.ToolTip = (text ?? "").Length > 100 ? text : null;
        SpeechBubble.Visibility = Visibility.Visible;
        SpeechBubble.Opacity = 0;
        SpeechBubble.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));

        var token = ++_bubbleToken;
        _ = HideSpeechAfterDelayAsync(token, greeting ? 6500 : 5500);
    }

    private async Task HideSpeechAfterDelayAsync(int token, int delayMilliseconds)
    {
        try
        {
            await Task.Delay(delayMilliseconds);
        }
        catch
        {
            return;
        }

        if (_closing || token != _bubbleToken) return;
        await Dispatcher.InvokeAsync(() =>
        {
            if (token != _bubbleToken) return;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) =>
            {
                if (token == _bubbleToken) SpeechBubble.Visibility = Visibility.Collapsed;
            };
            SpeechBubble.BeginAnimation(OpacityProperty, fade);
        });
    }

    private void OnCharacterMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var cursor = Native.GetCursorPosition();
        _mouseDown = true;
        _dragging = false;
        _dragCursorX = cursor.X;
        _dragCursorY = cursor.Y;
        _dragStartLeft = Left;
        _dragStartTop = Top;
        CharacterHost.CaptureMouse();
        e.Handled = true;
    }

    private void OnCharacterMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
        var cursor = Native.GetCursorPosition();
        var dx = cursor.X - _dragCursorX;
        var dy = cursor.Y - _dragCursorY;
        if (!_dragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4)) _dragging = true;
        if (!_dragging) return;

        Left = ClampLeft(_dragStartLeft + dx);
        Top = ClampTop(_dragStartTop + dy);
        _anchorBottom = Top + ActualHeight;
        PositionWorkBadge();
        e.Handled = true;
    }

    private void OnCharacterMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        CharacterHost.ReleaseMouseCapture();
        var shouldInteract = _mouseDown && !_dragging;
        _mouseDown = false;
        _dragging = false;
        if (shouldInteract)
        {
            Interact();
        }
        else
        {
            SaveWindowPosition();
            TryCollapseAtEdge();
        }
        e.Handled = true;
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_settings.AutoHideAtEdge || _collapsedAtEdge || _edgeSide == EdgeSide.None || _suppressEdgeCollapse) return;
        _edgeLeaveTimer.Stop();
        _edgeLeaveTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.EdgeHoverSeconds, 1, 10));
        _edgeLeaveTimer.Start();
    }

    private void OnEdgeLeaveTimer(object? sender, EventArgs e)
    {
        _edgeLeaveTimer.Stop();
        if (_collapsedAtEdge || _suppressEdgeCollapse || !_settings.AutoHideAtEdge || IsMouseOver) return;
        CollapseToEdge(_edgeSide);
    }

    private void OnCollapsedTabMouseEnter(object sender, MouseEventArgs e)
    {
        ExpandFromEdge();
    }

    private void TryCollapseAtEdge()
    {
        if (!_settings.AutoHideAtEdge || _collapsedAtEdge) return;
        var area = SystemParameters.WorkArea;
        var threshold = Math.Clamp(_settings.EdgeCollapseDistance, 0, 50);
        if (Left <= area.Left + threshold) _edgeSide = EdgeSide.Left;
        else if (Left + Width >= area.Right - threshold) _edgeSide = EdgeSide.Right;
        else if (Top <= area.Top + threshold) _edgeSide = EdgeSide.Top;
        else if (Top + Height >= area.Bottom - threshold) _edgeSide = EdgeSide.Bottom;
        else _edgeSide = EdgeSide.None;

        if (_edgeSide != EdgeSide.None) CollapseToEdge(_edgeSide);
    }

    private void CollapseToEdge(EdgeSide side)
    {
        if (_collapsedAtEdge || side == EdgeSide.None) return;
        _expandedBounds = new Rect(Left, Top, Width, Height);
        _edgeSide = side;
        _collapsedAtEdge = true;
        Root.Visibility = Visibility.Collapsed;
        _workBadgeWindow?.HideBadge();
        CollapsedTab.Visibility = Visibility.Visible;
        CollapsedTab.Opacity = 0;

        var area = SystemParameters.WorkArea;
        var tabWidth = side is EdgeSide.Left or EdgeSide.Right ? 30d : 76d;
        var tabHeight = side is EdgeSide.Left or EdgeSide.Right ? 76d : 38d;
        SizeToContent = SizeToContent.Manual;
        Width = tabWidth;
        Height = tabHeight;
        Left = side == EdgeSide.Left ? area.Left : side == EdgeSide.Right ? area.Right - tabWidth : _expandedBounds.Left;
        Top = side == EdgeSide.Top ? area.Top : side == EdgeSide.Bottom ? area.Bottom - tabHeight : _expandedBounds.Top;
        CollapsedTab.MinWidth = tabWidth;
        CollapsedTab.MinHeight = tabHeight;
        CollapsedHint.Text = side switch
        {
            EdgeSide.Left => "▶",
            EdgeSide.Right => "◀",
            EdgeSide.Top => "▼",
            _ => "▲",
        };
        CollapsedTab.CornerRadius = side switch
        {
            EdgeSide.Left => new CornerRadius(0, 8, 8, 0),
            EdgeSide.Right => new CornerRadius(8, 0, 0, 8),
            EdgeSide.Top => new CornerRadius(0, 0, 8, 8),
            _ => new CornerRadius(8, 8, 0, 0),
        };
        CollapsedTab.Opacity = 1;
        CollapsedTab.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            });
    }

    private void ExpandFromEdge()
    {
        if (!_collapsedAtEdge) return;
        _collapsedAtEdge = false;
        CollapsedTab.BeginAnimation(OpacityProperty, null);
        CollapsedTab.Visibility = Visibility.Collapsed;
        CollapsedTab.MinWidth = 0;
        CollapsedTab.MinHeight = 0;
        Root.Visibility = Visibility.Visible;
        Root.Opacity = 1;
        SizeToContent = SizeToContent.Manual;
        Width = _expandedBounds.Width;
        Height = _expandedBounds.Height;
        Left = _expandedBounds.Left;
        Top = _expandedBounds.Top;
        SizeToContent = SizeToContent.Height;
        Root.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            });
        _edgeLeaveTimer.Stop();
        _edgeLeaveTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.EdgeHoverSeconds, 1, 10));
        _edgeLeaveTimer.Start();
    }

    private void OnCharacterMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_mouseDown || e.LeftButton == MouseButtonState.Pressed) return;
        _mouseDown = false;
        _dragging = false;
        CharacterHost.ReleaseMouseCapture();
    }

    private void Interact()
    {
        _lastInteraction = DateTime.Now;
        _playUntil = DateTime.Now.AddSeconds(_settings.PlaySeconds);
        SetState(PetState.Play, true);
        var phrases = _settings.InteractionPhrases;
        if (phrases.Count == 0) phrases = PetSettings.CreateDefault().InteractionPhrases;
        var checkedMemos = MemoStore.Load()
            .Where(memo => memo.Done && !string.IsNullOrWhiteSpace(memo.Text))
            .Select(memo => memo.Text)
            .ToList();
        var pool = checkedMemos.Count > 0 && _random.Next(100) < 70 ? checkedMemos : phrases;
        ShowSpeech(pool[_random.Next(pool.Count)]);
        UpdateStatusText();
    }

    private async Task RefreshWeatherAsync()
    {
        if (!_settings.WeatherEnabled)
        {
            WeatherPill.Visibility = Visibility.Collapsed;
        }

        _lastWeatherFetch = DateTime.Now;
        _weatherCts?.Cancel();
        _weatherCts?.Dispose();
        _weatherCts = new CancellationTokenSource();
        var token = _weatherCts.Token;
        if (_weatherInfo is null)
        {
            WeatherPill.Visibility = _settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
            WeatherText.Text = "正在获取天气…";
        }

        try
        {
            var info = await _weatherService.GetAsync(_settings, token);
            if (token.IsCancellationRequested) return;
            if (info is null)
            {
                _weatherInfo = null;
                WeatherPill.Visibility = _settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
                WeatherText.Text = "天气获取失败";
                WeatherDetailText.Text = "天气不可用：当前离线或网络受限。";
                return;
            }

            _weatherInfo = info;
            WeatherPill.Visibility = _settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
            WeatherText.Text = info.Summary;
            WeatherDetailText.Text = info.Detail;
            UpdateStatusText();
        }
        catch
        {
            if (!token.IsCancellationRequested)
            {
                WeatherPill.Visibility = _settings.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
                WeatherText.Text = "天气获取失败";
                WeatherDetailText.Text = "天气不可用：当前离线或网络受限。";
            }
        }
    }

    private void UpdateStatusText()
    {
        var idle = Native.GetIdleTime();
        var stateLabel = _state == PetState.Play ? "互动中" : _state == PetState.Idle ? "休眠中" : "待机中";
        var weatherPart = _weatherInfo is null
            ? ""
            : $" · {_weatherInfo.Icon} {Math.Round(_weatherInfo.Temperature)}°C";
        var memoPart = _memoCount > 0 ? $" · 📝 {_memoCount}" : "";
        SummaryText.Text = $"鲸鱼娘 · {DateTime.Now:HH:mm}{weatherPart} · {stateLabel}{memoPart}";

        ClockText.Text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} · 本机时间";
        StateText.Text = $"形象状态：{stateLabel} · 主形象 {_settings.MainImages.Count} 张 / 空闲 {_settings.IdleImages.Count} 张 / 互动 {_settings.PlayImages.Count} 张";
        var remain = Math.Max(0, _settings.IdleMinutes * 60 - idle.TotalSeconds);
        IdleText.Text = remain <= 0
            ? $"已空闲 {idle.TotalMinutes:0.0} 分钟 · 已达到休眠阈值"
            : $"空闲 {idle.TotalMinutes:0.0} 分钟 · 距离休眠约 {TimeSpan.FromSeconds(remain):mm\\:ss}";
        ConfigText.Text = "配置：" + SettingsStore.ConfigPath;
        SummaryChevron.Text = _detailsOpen ? "▴" : "▾";
        RefreshStatusMarquee();
    }

    private void OnSummaryClick(object sender, RoutedEventArgs e)
    {
        if (!_settings.ShowStatusBar) return;
        _detailsOpen = !_detailsOpen;
        KeepBottomAnchored(() =>
        {
            DetailsPanel.Visibility = _detailsOpen ? Visibility.Visible : Visibility.Collapsed;
            SummaryChevron.Text = _detailsOpen ? "▴" : "▾";
        });
        if (_detailsOpen)
        {
            ApplyAppearance();
            UpdateStatusText();
        }
    }

    private void KeepBottomAnchored(Action action)
    {
        var previousHeight = ActualHeight;
        _anchorBottom = Top + previousHeight;
        action();
        UpdateLayout();
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            if (double.IsNaN(_anchorBottom)) return;
            _adjustingPosition = true;
            Top = ClampTop(_anchorBottom - ActualHeight);
            _adjustingPosition = false;
        }));
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _suppressEdgeCollapse = true;
        var dialog = new SettingsWindow(_settings.Clone()) { Owner = this };
        var accepted = false;
        try { accepted = dialog.ShowDialog() == true; }
        finally { _suppressEdgeCollapse = false; }
        if (!accepted) return;
        _settings = dialog.Settings;
        SettingsStore.Save(_settings);
        ApplySettingsToUi();
        _lastWeatherFetch = DateTime.MinValue;
        SetState(PetState.Main, true);
        _nextWaterReminder = DateTime.Now.AddMinutes(_settings.WaterReminderMinutes);
        _nextSedentary = DateTime.Now.AddMinutes(Math.Max(1, _settings.SedentaryMinutes));
        _alarmFiredDates.Clear();
        ShowSpeech("设置已保存。");
        UpdateStatusText();
        _ = RefreshWeatherAsync();
    }

    private void OnMemoClick(object sender, RoutedEventArgs e)
    {
        _suppressEdgeCollapse = true;
        try
        {
            var window = new MemoWindow(_settings) { Owner = this };
            window.ShowDialog();
        }
        finally
        {
            _suppressEdgeCollapse = false;
        }
        UpdateMemoCount();
        UpdateStatusText();
    }

    private void UpdateMemoCount()
    {
        _memoCount = MemoStore.Load().Count;
    }

    private void PollClipboard()
    {
        if (_closing || _clipboardPaused || !_settings.ClipboardHistoryEnabled) return;

        string text;
        try
        {
            if (!Clipboard.ContainsText()) return;
            text = Clipboard.GetText() ?? "";
        }
        catch
        {
            return;
        }

        text = text.Trim();
        if (text.Length == 0) return;
        if (!_clipboardPrimed)
        {
            _clipboardPrimed = true;
            _lastClipboardText = text;
            return;
        }
        if (string.Equals(text, _lastClipboardText, StringComparison.Ordinal)) return;

        _lastClipboardText = text;
        if (text.Length > 4000) text = text[..4000];

        _clipboardEntries.RemoveAll(entry => string.Equals(entry.Text, text, StringComparison.Ordinal));
        _clipboardEntries.Insert(0, new ClipboardEntry { Text = text, CreatedAt = DateTime.Now });
        while (_clipboardEntries.Count > ClipboardStore.MaxEntries)
        {
            _clipboardEntries.RemoveAt(_clipboardEntries.Count - 1);
        }
        ClipboardStore.Save(_clipboardEntries);
        ClipboardHistoryButton.ToolTip = $"剪贴板历史（{_clipboardEntries.Count} 条）";
    }

    private void OnClipboardHistoryClick(object sender, RoutedEventArgs e)
    {
        _suppressEdgeCollapse = true;
        _clipboardPaused = true;
        try
        {
            new ClipboardWindow { Owner = this }.ShowDialog();
        }
        finally
        {
            _clipboardPaused = false;
            _suppressEdgeCollapse = false;
        }

        _clipboardEntries.Clear();
        _clipboardEntries.AddRange(ClipboardStore.Load());
        ClipboardHistoryButton.ToolTip = $"剪贴板历史（{_clipboardEntries.Count} 条）";
    }

    private void OnLockScreenClick(object sender, RoutedEventArgs e)
    {
        if (!Native.LockScreen()) ShowSpeech("锁屏失败，请检查系统权限。");
    }

    private void OnAlarmsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AlarmWindow(_settings.Alarms) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _settings.Alarms = dialog.Alarms;
        SettingsStore.Save(_settings);
        _alarmFiredDates.Clear();
        ShowSpeech(_settings.Alarms.Count == 0
            ? "闹钟已清空。"
            : $"闹钟已更新（{_settings.Alarms.Count} 个）。");
        UpdateStatusText();
    }

    private void OnToggleStatusClick(object sender, RoutedEventArgs e)
    {
        _settings.ShowStatusBar = !_settings.ShowStatusBar;
        SettingsStore.Save(_settings);
        ApplySettingsToUi();
    }

    private void OnResetPositionClick(object sender, RoutedEventArgs e)
    {
        PlaceBottomRight();
        _anchorBottom = Top + ActualHeight;
        SaveWindowPosition();
    }

    private void OnConfigPathClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Clipboard.SetText(SettingsStore.ConfigPath);
            ShowSpeech("配置路径已复制。");
        }
        catch
        {
            // Clipboard can be locked by another process.
        }
        e.Handled = true;
    }

    private void OnConfigPathRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "打开配置所在文件夹" };
        open.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + SettingsStore.ConfigPath + "\"") { UseShellExecute = true });
            }
            catch
            {
                // Explorer is best effort.
            }
        };
        menu.Items.Add(open);
        menu.PlacementTarget = ConfigText;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnRefreshWeatherClick(object sender, RoutedEventArgs e)
    {
        _lastWeatherFetch = DateTime.MinValue;
        _ = RefreshWeatherAsync();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void ApplySettingsToUi()
    {
        _settings.Normalize();
        SummaryButton.Visibility = _settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        if (!_settings.ShowStatusBar)
        {
            _detailsOpen = false;
            DetailsPanel.Visibility = Visibility.Collapsed;
        }
        WeatherPill.Visibility = _settings.WeatherEnabled && _weatherInfo is not null ? Visibility.Visible : Visibility.Collapsed;
        GearButton.Visibility = _settings.ShowSettingsButton ? Visibility.Visible : Visibility.Collapsed;
        SummaryChevron.Text = _detailsOpen ? "▴" : "▾";
        ApplyScale();
        ApplyAppearance();
        _workBadgeWindow?.Close();
        _workBadgeWindow = null;
        UpdateWorkState();
        UpdateWorkReminders();
        UpdateRemovableDevices();
        UpdateStatusActions();
        if (!_settings.AutoHideAtEdge && _collapsedAtEdge) ExpandFromEdge();
        if (_settings.ShowDeviceInfo) RefreshDeviceInfo();
        else DeviceInfoPanel.Visibility = Visibility.Collapsed;

        ClipboardHistoryButton.Visibility = _settings.ClipboardHistoryEnabled ? Visibility.Visible : Visibility.Collapsed;
        LockScreenButton.Visibility = _settings.LockScreenEnabled ? Visibility.Visible : Visibility.Collapsed;
        AlarmButton.Visibility = _settings.AlarmsEnabled ? Visibility.Visible : Visibility.Collapsed;
        ScreenshotButton.Visibility = _settings.ScreenshotEnabled ? Visibility.Visible : Visibility.Collapsed;
        ClipboardMenuItem.Visibility = ClipboardHistoryButton.Visibility;
        LockMenuItem.Visibility = LockScreenButton.Visibility;
        AlarmMenuItem.Visibility = AlarmButton.Visibility;
        ScreenshotMenuItem.Visibility = ScreenshotButton.Visibility;
        ClipboardHistoryButton.ToolTip = $"剪贴板历史（{_clipboardEntries.Count} 条）";
        RegisterScreenshotHotkey();
    }

    private void ApplyAppearance()
    {
        var textBrush = Brush(_settings.StatusTextColor);
        var theme = Brush(_settings.ThemeColor);
        SummaryText.Foreground = textBrush;
        SummaryText.FontSize = _settings.StatusFontSize;
        SummaryChevron.Foreground = textBrush;
        WeatherText.Foreground = textBrush;
        WeatherText.FontSize = _settings.WeatherFontSize;
        foreach (var text in FindVisualChildren<System.Windows.Controls.TextBlock>(DetailsPanel))
        {
            text.Foreground = textBrush;
            text.FontSize = Math.Max(8, _settings.StatusFontSize - 0.5);
        }
        foreach (var button in FindVisualChildren<Button>(DetailsPanel))
        {
            button.Foreground = textBrush;
            button.FontSize = Math.Max(8, _settings.StatusFontSize - 0.5);
        }
        SummaryButton.BorderBrush = theme;
        DetailsPanel.BorderBrush = theme;
        WeatherPill.BorderBrush = theme;
        GearButton.BorderBrush = theme;
        WorkBadge.BorderBrush = theme;

        var themeColor = ThemePalette.Parse(_settings.ThemeColor, Color.FromRgb(0x4D, 0x6B, 0xFE));
        var surface = ThemePalette.Surface(
            _settings.ThemeColor,
            _settings.BackgroundImageEnabled,
            _settings.BackgroundImagePath,
            _settings.BackgroundImageOpacity,
            0.22);
        SummaryButton.Background = surface;
        DetailsPanel.Background = surface;
        WeatherPill.Background = ThemePalette.Panel(_settings.ThemeColor, 0.88, 0.25);
        GearButton.Background = ThemePalette.Panel(_settings.ThemeColor, 0.95, 0.25);
        LockStatusButton.Background = ThemePalette.Panel(_settings.ThemeColor, 0.95, 0.25);
        UsbButton.Background = ThemePalette.Panel(_settings.ThemeColor, 0.95, 0.25);
        InputBadge.Background = ThemePalette.Panel(_settings.ThemeColor, 0.90, 0.25);
        CollapsedTab.Background = ThemePalette.Panel(_settings.ThemeColor, 0.95, 0.22);
        CollapsedTab.BorderBrush = theme;
        WorkBadge.Background = ThemePalette.Brush(
            ThemePalette.Blend(themeColor, Color.FromRgb(0xFF, 0xF8, 0xEA), 0.10), 0.95);

        Resources["MenuBackground"] = ThemePalette.Panel(_settings.ThemeColor, 0.98, 0.22);
        Resources["MenuBorder"] = ThemePalette.Brush(ThemePalette.Lighten(themeColor, 0.35), 0.80);
        Resources["MenuForeground"] = textBrush;
        Resources["MenuHover"] = ThemePalette.Brush(themeColor, 0.24);
        Resources["MenuSeparator"] = ThemePalette.Brush(themeColor, 0.35);

        WorkBadgeText.FontSize = _settings.WorkBadgeFontSize;
        WorkBadgeScale.ScaleX = _settings.WorkBadgeScale;
        WorkBadgeScale.ScaleY = _settings.WorkBadgeScale;
        InputBadge.Margin = new Thickness(8, 8, 0, 0);
        SettingsButtonScale.ScaleX = _settings.SettingsButtonScale;
        SettingsButtonScale.ScaleY = _settings.SettingsButtonScale;
        SpeechBubbleScale.ScaleX = _settings.SpeechBubbleScale;
        SpeechBubbleScale.ScaleY = _settings.SpeechBubbleScale;
        SpeechText.FontSize = _settings.SpeechBubbleFontSize;
        var rootWidth = Root.ActualWidth > 10 ? Root.ActualWidth : 430;
        var availableBubble = Math.Max(150, Math.Min(310, rootWidth - 24));
        SpeechBubble.MaxWidth = Math.Max(150, availableBubble / Math.Max(0.5, _settings.SpeechBubbleScale));
        InputBadgeScale.ScaleX = _settings.InputBadgeScale;
        InputBadgeScale.ScaleY = _settings.InputBadgeScale;
    }

    private static SolidColorBrush Brush(string color)
    {
        try
        {
            var parsed = (Color)ColorConverter.ConvertFromString(color);
            var brush = new SolidColorBrush(parsed);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return new SolidColorBrush(Colors.White);
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private void ApplyScale()
    {
        var previousBottom = _loaded && !double.IsNaN(Top) ? Top + ActualHeight : double.NaN;
        RootScale.ScaleX = _settings.Scale;
        RootScale.ScaleY = _settings.Scale;
        CharacterLayoutScale.ScaleX = _settings.CharacterScale;
        CharacterLayoutScale.ScaleY = _settings.CharacterScale;
        ControlLayer.Width = 430 * _settings.CharacterScale;
        var statusWidth = 430 * _settings.StatusBarScale;
        SummaryButton.Width = statusWidth;
        SummaryButton.MinHeight = Math.Max(38 * _settings.StatusBarScale, _settings.StatusFontSize + 18);
        DetailsPanel.Width = statusWidth;
        var contentWidth = Math.Max(430 * _settings.CharacterScale, Math.Max(430, statusWidth));
        Width = contentWidth * _settings.Scale;
        UpdateLayout();
        PositionStatusActions();
        RefreshStatusMarquee();
        if (!double.IsNaN(previousBottom))
        {
            _adjustingPosition = true;
            Top = ClampTop(previousBottom - ActualHeight);
            Left = ClampLeft(Left);
            _adjustingPosition = false;
            _anchorBottom = Top + ActualHeight;
        }
    }

    private void RestorePosition()
    {
        if (_settings.WindowLeft is { } savedLeft
            && _settings.WindowTop is { } savedTop
            && IsVisibleOnScreen(savedLeft, savedTop))
        {
            Left = ClampLeft(savedLeft);
            Top = ClampTop(savedTop);
        }
        else
        {
            PlaceBottomRight();
        }
        _anchorBottom = Top + ActualHeight;
    }

    private void PlaceBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = ClampLeft(area.Right - ActualWidth);
        Top = ClampTop(area.Bottom - ActualHeight);
        _anchorBottom = Top + ActualHeight;
    }

    private bool IsVisibleOnScreen(double left, double top)
    {
        return left > SystemParameters.VirtualScreenLeft - 40
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80
            && top > SystemParameters.VirtualScreenTop - 40
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80;
    }

    private double ClampLeft(double value)
    {
        var min = SystemParameters.VirtualScreenLeft;
        var max = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Math.Max(120, ActualWidth);
        return Math.Clamp(value, min, Math.Max(min, max));
    }

    private double ClampTop(double value)
    {
        var min = SystemParameters.VirtualScreenTop;
        var max = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Math.Max(120, ActualHeight);
        return Math.Clamp(value, min, Math.Max(min, max));
    }

    private void SaveWindowPosition()
    {
        if (_closing || double.IsNaN(Left) || double.IsNaN(Top)) return;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        try
        {
            SettingsStore.Save(_settings);
        }
        catch
        {
            // A read-only profile should not break the pet.
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_loaded || _adjustingPosition || double.IsNaN(_anchorBottom)) return;
        if (e.HeightChanged)
        {
            _adjustingPosition = true;
            Top = ClampTop(_anchorBottom - ActualHeight);
            _adjustingPosition = false;
        }
        if (e.WidthChanged || e.HeightChanged) PositionStatusActions();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        _timer.Stop();
        _inputTimer.Stop();
        _deviceTimer.Stop();
        _clipboardTimer.Stop();
        _statusMarqueeTimer.Stop();
        _edgeLeaveTimer.Stop();
        _workBadgeWindow?.Close();
        _workBadgeWindow = null;
        _alarmPopupWindow?.Close();
        _alarmPopupWindow = null;
        _inputHook?.Dispose();
        HotkeyManager.Unregister(_hwnd, ScreenshotHotkeyId);
        _hwndSource?.RemoveHook(WndProc);
        _weatherCts?.Cancel();
        _weatherCts?.Dispose();
        SaveWindowPosition();
    }
}
