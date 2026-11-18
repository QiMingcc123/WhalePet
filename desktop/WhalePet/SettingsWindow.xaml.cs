using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace WhalePet;

public partial class SettingsWindow : Window
{
    public PetSettings Settings { get; private set; }
    private bool _initialScrollLock = true;
    private List<AlarmItem> _alarms = new();

    public SettingsWindow(PetSettings settings)
    {
        InitializeComponent();
        Settings = settings.Clone();
        ShowStatusCheck.IsChecked = settings.ShowStatusBar;
        GreetingCheck.IsChecked = settings.GreetingEnabled;
        AutoStartCheck.IsChecked = settings.StartWithWindows;
        ShowSettingsButtonCheck.IsChecked = settings.ShowSettingsButton;
        WeatherCheck.IsChecked = settings.WeatherEnabled;
        WeatherCityBox.Text = settings.WeatherCity;
        WeatherFontSizeBox.Text = settings.WeatherFontSize.ToString(CultureInfo.InvariantCulture);
        StatusBarScaleBox.Text = settings.StatusBarScale.ToString(CultureInfo.InvariantCulture);
        MemoUiScaleBox.Text = settings.MemoUiScale.ToString(CultureInfo.InvariantCulture);
        ShowDeviceInfoCheck.IsChecked = settings.ShowDeviceInfo;
        ShowRemovableDevicesCheck.IsChecked = settings.ShowRemovableDevices;
        ShowWaterReminderCheck.IsChecked = settings.ShowWaterReminder;
        WaterPopupCheck.IsChecked = settings.WaterPopupEnabled;
        AutoHideCheck.IsChecked = settings.AutoHideAtEdge;
        EdgeDistanceBox.Text = settings.EdgeCollapseDistance.ToString(CultureInfo.InvariantCulture);
        EdgeHoverSecondsBox.Text = settings.EdgeHoverSeconds.ToString(CultureInfo.InvariantCulture);
        WaterMinutesBox.Text = settings.WaterReminderMinutes.ToString(CultureInfo.InvariantCulture);
        SedentaryCheck.IsChecked = settings.SedentaryReminderEnabled;
        SedentaryPopupCheck.IsChecked = settings.SedentaryPopupEnabled;
        SedentaryMinutesBox.Text = settings.SedentaryMinutes.ToString(CultureInfo.InvariantCulture);
        AlarmsCheck.IsChecked = settings.AlarmsEnabled;
        _alarms = settings.Alarms.Select(alarm => alarm.Clone()).ToList();
        UpdateAlarmSummary();
        ClipboardCheck.IsChecked = settings.ClipboardHistoryEnabled;
        LockScreenCheck.IsChecked = settings.LockScreenEnabled;
        BackgroundImageCheck.IsChecked = settings.BackgroundImageEnabled;
        BackgroundImageBox.Text = settings.BackgroundImagePath;
        BackgroundOpacityBox.Text = settings.BackgroundImageOpacity.ToString(CultureInfo.InvariantCulture);
        ScreenshotCheck.IsChecked = settings.ScreenshotEnabled;
        ScreenshotCtrlCheck.IsChecked = settings.ScreenshotCtrl;
        ScreenshotAltCheck.IsChecked = settings.ScreenshotAlt;
        ScreenshotShiftCheck.IsChecked = settings.ScreenshotShift;
        ScreenshotWinCheck.IsChecked = settings.ScreenshotWin;
        ScreenshotKeyBox.Text = settings.ScreenshotKey;
        WorkMonCheck.IsChecked = settings.WorkDays.Contains(1);
        WorkTueCheck.IsChecked = settings.WorkDays.Contains(2);
        WorkWedCheck.IsChecked = settings.WorkDays.Contains(3);
        WorkThuCheck.IsChecked = settings.WorkDays.Contains(4);
        WorkFriCheck.IsChecked = settings.WorkDays.Contains(5);
        WorkSatCheck.IsChecked = settings.WorkDays.Contains(6);
        WorkSunCheck.IsChecked = settings.WorkDays.Contains(0);
        WorkStartBox.Text = settings.WorkStart;
        WorkEndBox.Text = settings.WorkEnd;
        ShowWorkBadgeCheck.IsChecked = settings.ShowWorkBadge;
        WorkBadgeTextBox.Text = settings.WorkBadgeText;
        WorkBadgeScaleBox.Text = settings.WorkBadgeScale.ToString(CultureInfo.InvariantCulture);
        WorkBadgeFontSizeBox.Text = settings.WorkBadgeFontSize.ToString(CultureInfo.InvariantCulture);
        ShowBeforeWorkReminderCheck.IsChecked = settings.ShowBeforeWorkReminder;
        BeforeWorkMinutesBox.Text = settings.BeforeWorkReminderMinutes.ToString(CultureInfo.InvariantCulture);
        ShowEndWorkReminderCheck.IsChecked = settings.ShowEndWorkReminder;
        WorkBeforeReminderBox.Text = settings.WorkReminderBeforeText;
        WorkEndReminderBox.Text = settings.WorkReminderEndText;
        IdleMinutesBox.Text = settings.IdleMinutes.ToString(CultureInfo.InvariantCulture);
        PlaySecondsBox.Text = settings.PlaySeconds.ToString(CultureInfo.InvariantCulture);
        TransitionBox.Text = settings.TransitionMs.ToString(CultureInfo.InvariantCulture);
        ScaleBox.Text = settings.Scale.ToString(CultureInfo.InvariantCulture);
        CharacterScaleBox.Text = settings.CharacterScale.ToString(CultureInfo.InvariantCulture);
        StatusFontSizeBox.Text = settings.StatusFontSize.ToString(CultureInfo.InvariantCulture);
        SettingsFontSizeBox.Text = settings.SettingsFontSize.ToString(CultureInfo.InvariantCulture);
        MemoFontSizeBox.Text = settings.MemoFontSize.ToString(CultureInfo.InvariantCulture);
        ThemeColorBox.Text = settings.ThemeColor;
        StatusTextColorBox.Text = settings.StatusTextColor;
        SettingsTextColorBox.Text = settings.SettingsTextColor;
        MemoTextColorBox.Text = settings.MemoTextColor;
        SettingsButtonScaleBox.Text = settings.SettingsButtonScale.ToString(CultureInfo.InvariantCulture);
        SpeechBubbleScaleBox.Text = settings.SpeechBubbleScale.ToString(CultureInfo.InvariantCulture);
        SpeechBubbleScaleBox.Text = settings.SpeechBubbleScale.ToString(CultureInfo.InvariantCulture);
        SpeechBubbleFontSizeBox.Text = settings.SpeechBubbleFontSize.ToString(CultureInfo.InvariantCulture);
        InputBadgeScaleBox.Text = settings.InputBadgeScale.ToString(CultureInfo.InvariantCulture);
        ChatIdleMinutesBox.Text = settings.ChatIdleMinutes.ToString(CultureInfo.InvariantCulture);
        MainImagesBox.Text = string.Join(Environment.NewLine, settings.MainImages);
        IdleImagesBox.Text = string.Join(Environment.NewLine, settings.IdleImages);
        PlayImagesBox.Text = string.Join(Environment.NewLine, settings.PlayImages);
        PhrasesBox.Text = string.Join(Environment.NewLine, settings.InteractionPhrases);
        ChatPhrasesBox.Text = string.Join(Environment.NewLine, settings.ChatPhrases);
        ConfigHint.Text = "配置保存位置：" + SettingsStore.ConfigPath;
        ConfigHint.ToolTip = "本机：" + SettingsStore.ConfigPath
            + "\n漫游备份：" + SettingsStore.BackupPath
            + "\n用户目录：" + PersistentStore.HomePath("settings.json")
            + "\n自动备份：" + PersistentStore.BackupDirectory;
        UpdateWeatherLocationState();
        Loaded += (_, _) =>
        {
            ApplyAppearance();
            SettingsScroll.ScrollToTop();
            var settle = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1600),
            };
            settle.Tick += (_, _) =>
            {
                settle.Stop();
                _initialScrollLock = false;
                System.Windows.Input.Keyboard.ClearFocus();
                SettingsScroll.ScrollToTop();
                SettingsScroll.ScrollToVerticalOffset(0);
            };
            settle.Start();
        };
        ContentRendered += (_, _) =>
        {
            System.Windows.Input.Keyboard.ClearFocus();
            SettingsScroll.ScrollToTop();
        };
    }

    private void ApplyAppearance()
    {
        var textBrush = Brush(Settings.SettingsTextColor);
        var delta = Settings.SettingsFontSize - 11;
        foreach (var text in FindVisualChildren<System.Windows.Controls.TextBlock>(this))
        {
            if (ReferenceEquals(text, ErrorText)) continue;
            text.Foreground = textBrush;
            text.FontSize = Math.Clamp(text.FontSize + delta, 8, 40);
        }
        foreach (var box in FindVisualChildren<System.Windows.Controls.TextBox>(this))
        {
            box.Foreground = textBrush;
            box.FontSize = Math.Clamp(box.FontSize + delta, 8, 40);
        }
        foreach (var check in FindVisualChildren<System.Windows.Controls.CheckBox>(this))
        {
            check.Foreground = textBrush;
            check.FontSize = Math.Clamp(check.FontSize + delta, 8, 40);
        }
        var theme = Brush(Settings.ThemeColor);
        SaveButton.Background = theme;
        SaveButton.BorderBrush = theme;
        NoticeBubble.BorderBrush = theme;
        Background = ThemePalette.Surface(
            Settings.ThemeColor,
            Settings.BackgroundImageEnabled,
            Settings.BackgroundImagePath,
            Settings.BackgroundImageOpacity,
            0.14);
    }

    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: System.Windows.Controls.TextBox box }) return;
        var dialog = new ColorPickerWindow(box.Text) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            box.Text = dialog.SelectedColor;
        }
    }

    private void OnBrowseBackground(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|所有文件|*.*",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true) return;
        BackgroundImageBox.Text = dialog.FileName;
        BackgroundImageCheck.IsChecked = true;
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

    private static bool TryColor(string text, out string normalized)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(text.Trim());
            normalized = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            return true;
        }
        catch
        {
            normalized = "";
            return false;
        }
    }

    private void OnSettingsScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (!_initialScrollLock) return;
        if (Math.Abs(SettingsScroll.VerticalOffset) > 0.5)
        {
            SettingsScroll.ScrollToTop();
        }
    }

    private void ShowNotice(string message)
    {
        ErrorText.Text = message;
        NoticeBubble.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnWeatherChanged(object sender, RoutedEventArgs e) => UpdateWeatherLocationState();

    private void OnEditAlarms(object sender, RoutedEventArgs e)
    {
        var dialog = new AlarmWindow(_alarms) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _alarms = dialog.Alarms;
            UpdateAlarmSummary();
        }
    }

    private void UpdateAlarmSummary()
    {
        if (_alarms.Count == 0)
        {
            AlarmSummary.Text = "还没有闹钟，点右侧按钮添加。";
            return;
        }

        var preview = string.Join("、", _alarms
            .Take(4)
            .Select(alarm =>
            {
                var date = alarm.IsDaily ? "每天 " : alarm.Date + " ";
                var range = alarm.Start + (string.IsNullOrWhiteSpace(alarm.End) ? "" : "-" + alarm.End);
                return date + range;
            }));
        AlarmSummary.Text = $"已设置 {_alarms.Count} 个闹钟：{preview}" + (_alarms.Count > 4 ? "…" : "");
    }

    private void UpdateWeatherLocationState()
    {
        // The city setting is independent of whether the weather pill is visible.
        WeatherCityBox.IsEnabled = true;
    }

    private void OnBrowseMain(object sender, RoutedEventArgs e) => BrowseInto(MainImagesBox);

    private void OnBrowseIdle(object sender, RoutedEventArgs e) => BrowseInto(IdleImagesBox);

    private void OnBrowsePlay(object sender, RoutedEventArgs e) => BrowseInto(PlayImagesBox);

    private static void BrowseInto(System.Windows.Controls.TextBox box)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择角色图片",
            Filter = "图片文件|*.png;*.webp;*.jpg;*.jpeg;*.gif;*.bmp|所有文件|*.*",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true) return;
        var current = box.Text.TrimEnd();
        box.Text = current.Length == 0 ? dialog.FileName : current + Environment.NewLine + dialog.FileName;
        box.CaretIndex = box.Text.Length;
        box.ScrollToEnd();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ShowNotice("");
        if (!int.TryParse(IdleMinutesBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var idleMinutes)
            || idleMinutes < 1 || idleMinutes > 240)
        {
            ShowNotice("空闲判定必须是 1–240 分钟。");
            return;
        }
        if (!int.TryParse(PlaySecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var playSeconds)
            || playSeconds < 5 || playSeconds > 600)
        {
            ShowNotice("互动形象持续必须是 5–600 秒。");
            return;
        }
        if (!int.TryParse(TransitionBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var transitionMs)
            || transitionMs < 120 || transitionMs > 2000)
        {
            ShowNotice("切换动画必须是 120–2000 毫秒。");
            return;
        }
        if (!double.TryParse(ScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            || scale < 0.1 || scale > 1.5)
        {
            ShowNotice("界面大小必须在 0.1–1.5 之间。");
            return;
        }
        if (!double.TryParse(CharacterScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var characterScale)
            || characterScale < 0.1 || characterScale > 1.5)
        {
            ShowNotice("角色大小必须在 0.1–1.5 之间。");
            return;
        }
        if (!double.TryParse(StatusFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var statusFontSize)
            || statusFontSize < 8 || statusFontSize > 32
            || !double.TryParse(SettingsFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var settingsFontSize)
            || settingsFontSize < 8 || settingsFontSize > 32
            || !double.TryParse(MemoFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var memoFontSize)
            || memoFontSize < 8 || memoFontSize > 32
            || !double.TryParse(WeatherFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var weatherFontSize)
            || weatherFontSize < 8 || weatherFontSize > 32
            || !double.TryParse(SpeechBubbleFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speechBubbleFontSize)
            || speechBubbleFontSize < 8 || speechBubbleFontSize > 32)
        {
            ShowNotice("文字大小必须在 8–32 px 之间。");
            return;
        }
        if (!PetSettings.IsSingleCity(WeatherCityBox.Text))
        {
            ShowNotice("天气位置只能填写一个城市，例如：杭州。");
            return;
        }
        if (!TryColor(ThemeColorBox.Text, out var themeColor)
            || !TryColor(StatusTextColorBox.Text, out var statusTextColor)
            || !TryColor(SettingsTextColorBox.Text, out var settingsTextColor)
            || !TryColor(MemoTextColorBox.Text, out var memoTextColor))
        {
            ShowNotice("颜色请使用 #RRGGBB 格式，例如 #4D6BFE。");
            return;
        }
        if (!double.TryParse(SettingsButtonScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var settingsButtonScale)
            || settingsButtonScale < 0.5 || settingsButtonScale > 2
            || !double.TryParse(SpeechBubbleScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speechBubbleScale)
            || speechBubbleScale < 0.5 || speechBubbleScale > 2
            || !double.TryParse(InputBadgeScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var inputBadgeScale)
            || inputBadgeScale < 0.5 || inputBadgeScale > 2)
        {
            ShowNotice("控件大小必须在 0.5–2 之间。");
            return;
        }
        if (!double.TryParse(StatusBarScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var statusBarScale)
            || statusBarScale < 0.5 || statusBarScale > 2
            || !double.TryParse(MemoUiScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var memoUiScale)
            || memoUiScale < 0.5 || memoUiScale > 2)
        {
            ShowNotice("状态栏大小和备忘录界面大小必须在 0.5–2 之间。");
            return;
        }
        if (!int.TryParse(EdgeDistanceBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var edgeDistance)
            || edgeDistance < 0 || edgeDistance > 50)
        {
            ShowNotice("自动收起边缘距离必须在 0–50px 之间。");
            return;
        }
        if (!double.TryParse(EdgeHoverSecondsBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var edgeHoverSeconds)
            || edgeHoverSeconds < 1 || edgeHoverSeconds > 10)
        {
            ShowNotice("悬停展开保持时间必须在 1–10 秒之间。");
            return;
        }
        if (!int.TryParse(WaterMinutesBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var waterMinutes)
            || waterMinutes < 5 || waterMinutes > 240)
        {
            ShowNotice("喝水提醒间隔必须在 5–240 分钟之间。");
            return;
        }
        var sedentaryText = SedentaryMinutesBox.Text.Trim();
        if (sedentaryText.Length == 0) sedentaryText = "0";
        if (!int.TryParse(sedentaryText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sedentaryMinutes)
            || sedentaryMinutes < 0 || sedentaryMinutes > 240)
        {
            ShowNotice("久坐提醒间隔必须是 0–240 分钟（0=关闭）。");
            return;
        }
        var backgroundPath = BackgroundImageBox.Text.Trim();
        if (!double.TryParse(BackgroundOpacityBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var backgroundOpacity)
            || backgroundOpacity < 0 || backgroundOpacity > 1)
        {
            ShowNotice("背景图片透明度必须在 0–1 之间。");
            return;
        }
        if (BackgroundImageCheck.IsChecked == true)
        {
            if (backgroundPath.Length == 0)
            {
                ShowNotice("请选择背景图片文件。");
                return;
            }
            if (!File.Exists(backgroundPath))
            {
                ShowNotice("背景图片文件不存在，请重新选择。");
                return;
            }
        }
        var screenshotKey = (ScreenshotKeyBox.Text ?? "").Trim().ToUpperInvariant();
        if (!PetSettings.ScreenshotKeys.Contains(screenshotKey))
        {
            ShowNotice("截图快捷键的按键无效，请填 A–Z、0–9 或 F1–F12。");
            return;
        }
        if (!double.TryParse(WorkBadgeScaleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var workBadgeScale)
            || workBadgeScale < 0.5 || workBadgeScale > 2
            || !double.TryParse(WorkBadgeFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var workBadgeFontSize)
            || workBadgeFontSize < 8 || workBadgeFontSize > 32)
        {
            ShowNotice("搬砖提示框大小必须在 0.5–2，文字大小必须在 8–32 px。");
            return;
        }
        if (!int.TryParse(BeforeWorkMinutesBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var beforeWorkMinutes)
            || beforeWorkMinutes < 1 || beforeWorkMinutes > 120)
        {
            ShowNotice("下班前提醒时间必须在 1–120 分钟之间。");
            return;
        }
        if (!int.TryParse(ChatIdleMinutesBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatIdleMinutes)
            || chatIdleMinutes < 0 || chatIdleMinutes > 240)
        {
            ShowNotice("主动聊天间隔必须是 0–240 分钟。");
            return;
        }

        if (!TimeSpan.TryParseExact(WorkStartBox.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var workStart)
            || !TimeSpan.TryParseExact(WorkEndBox.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var workEnd))
        {
            ShowNotice("上班/下班时间请使用 HH:mm 格式，例如 09:00。");
            return;
        }

        var workDays = new List<int>();
        if (WorkMonCheck.IsChecked == true) workDays.Add(1);
        if (WorkTueCheck.IsChecked == true) workDays.Add(2);
        if (WorkWedCheck.IsChecked == true) workDays.Add(3);
        if (WorkThuCheck.IsChecked == true) workDays.Add(4);
        if (WorkFriCheck.IsChecked == true) workDays.Add(5);
        if (WorkSatCheck.IsChecked == true) workDays.Add(6);
        if (WorkSunCheck.IsChecked == true) workDays.Add(0);

        var main = SplitLines(MainImagesBox.Text);
        var idle = SplitLines(IdleImagesBox.Text);
        var play = SplitLines(PlayImagesBox.Text);
        var phrases = SplitLines(PhrasesBox.Text);
        var chatPhrases = SplitLines(ChatPhrasesBox.Text);
        if (main.Count < 1 || idle.Count < 1 || play.Count < 1)
        {
            ShowNotice("主形象、空闲形象、互动形象都至少需要 1 张图片。");
            return;
        }
        if (phrases.Count < 1)
        {
            ShowNotice("互动短句至少需要 1 条。");
            return;
        }
        if (chatIdleMinutes > 0 && chatPhrases.Count < 1)
        {
            ShowNotice("主动聊天间隔大于 0 时，至少需要 1 条主动聊天内容。");
            return;
        }

        var startWithWindows = AutoStartCheck.IsChecked == true;
        if (!AutoStartService.SetEnabled(startWithWindows, out var autoStartError))
        {
            ShowNotice(autoStartError ?? "无法修改开机自启动设置。");
            return;
        }

        Settings.ShowStatusBar = ShowStatusCheck.IsChecked == true;
        Settings.GreetingEnabled = GreetingCheck.IsChecked == true;
        Settings.StartWithWindows = startWithWindows;
        Settings.ShowSettingsButton = ShowSettingsButtonCheck.IsChecked == true;
        Settings.WeatherEnabled = WeatherCheck.IsChecked == true;
        Settings.WeatherCity = PetSettings.NormalizeCity(WeatherCityBox.Text);
        Settings.ShowDeviceInfo = ShowDeviceInfoCheck.IsChecked == true;
        Settings.ShowRemovableDevices = ShowRemovableDevicesCheck.IsChecked == true;
        Settings.ShowWaterReminder = ShowWaterReminderCheck.IsChecked == true;
        Settings.WaterPopupEnabled = WaterPopupCheck.IsChecked == true;
        Settings.AutoHideAtEdge = AutoHideCheck.IsChecked == true;
        Settings.EdgeCollapseDistance = edgeDistance;
        Settings.EdgeHoverSeconds = edgeHoverSeconds;
        Settings.WaterReminderMinutes = waterMinutes;
        Settings.SedentaryReminderEnabled = SedentaryCheck.IsChecked == true;
        Settings.SedentaryPopupEnabled = SedentaryPopupCheck.IsChecked == true;
        Settings.SedentaryMinutes = sedentaryMinutes;
        Settings.AlarmsEnabled = AlarmsCheck.IsChecked == true;
        Settings.Alarms = _alarms.Select(alarm => alarm.Clone()).ToList();
        Settings.ClipboardHistoryEnabled = ClipboardCheck.IsChecked == true;
        Settings.LockScreenEnabled = LockScreenCheck.IsChecked == true;
        Settings.BackgroundImageEnabled = BackgroundImageCheck.IsChecked == true;
        Settings.BackgroundImagePath = backgroundPath;
        Settings.BackgroundImageOpacity = backgroundOpacity;
        Settings.ScreenshotEnabled = ScreenshotCheck.IsChecked == true;
        Settings.ScreenshotCtrl = ScreenshotCtrlCheck.IsChecked == true;
        Settings.ScreenshotAlt = ScreenshotAltCheck.IsChecked == true;
        Settings.ScreenshotShift = ScreenshotShiftCheck.IsChecked == true;
        Settings.ScreenshotWin = ScreenshotWinCheck.IsChecked == true;
        Settings.ScreenshotKey = screenshotKey;
        Settings.WorkDays = workDays;
        Settings.WorkStart = workStart.ToString(@"hh\:mm");
        Settings.WorkEnd = workEnd.ToString(@"hh\:mm");
        Settings.ShowWorkBadge = ShowWorkBadgeCheck.IsChecked == true;
        Settings.WorkBadgeText = WorkBadgeTextBox.Text.Trim();
        Settings.WorkBadgeScale = workBadgeScale;
        Settings.WorkBadgeFontSize = workBadgeFontSize;
        Settings.ShowBeforeWorkReminder = ShowBeforeWorkReminderCheck.IsChecked == true;
        Settings.BeforeWorkReminderMinutes = beforeWorkMinutes;
        Settings.ShowEndWorkReminder = ShowEndWorkReminderCheck.IsChecked == true;
        Settings.WorkReminderBeforeText = WorkBeforeReminderBox.Text.Trim();
        Settings.WorkReminderEndText = WorkEndReminderBox.Text.Trim();
        Settings.IdleMinutes = idleMinutes;
        Settings.PlaySeconds = playSeconds;
        Settings.TransitionMs = transitionMs;
        Settings.Scale = scale;
        Settings.CharacterScale = characterScale;
        Settings.StatusBarScale = statusBarScale;
        Settings.MemoUiScale = memoUiScale;
        Settings.StatusFontSize = statusFontSize;
        Settings.SettingsFontSize = settingsFontSize;
        Settings.MemoFontSize = memoFontSize;
        Settings.ThemeColor = themeColor;
        Settings.StatusTextColor = statusTextColor;
        Settings.SettingsTextColor = settingsTextColor;
        Settings.MemoTextColor = memoTextColor;
        Settings.WeatherFontSize = weatherFontSize;
        Settings.SettingsButtonScale = settingsButtonScale;
        Settings.SpeechBubbleScale = speechBubbleScale;
        Settings.SpeechBubbleFontSize = speechBubbleFontSize;
        Settings.InputBadgeScale = inputBadgeScale;
        Settings.ChatIdleMinutes = chatIdleMinutes;
        Settings.MainImages = main;
        Settings.IdleImages = idle;
        Settings.PlayImages = play;
        Settings.InteractionPhrases = phrases;
        Settings.ChatPhrases = chatPhrases.Count > 0 ? chatPhrases : PetSettings.CreateDefault().ChatPhrases;
        Settings.Normalize();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private static List<string> SplitLines(string text)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var value = line.Trim();
            if (value.Length == 0 || !seen.Add(value)) continue;
            result.Add(value);
            if (result.Count >= 24) break;
        }
        return result;
    }
}
