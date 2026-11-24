using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using WhalePet.Game;

namespace WhalePet;

public partial class SettingsWindow : Window
{
    public PetSettings Settings { get; private set; }

    /// <summary>Live preview for the game overlay sliders; saving still happens on 淇濆瓨.</summary>
    public event Action<PetSettings>? PreviewChanged;
    public event Action<PetSettings>? ReadingPreviewChanged;
    public event Action<PetSettings>? SettingsSaved;
    public event Func<(double? Left, double? Top)>? ResetReadingPositionRequested;
    public bool HasSavedSettings { get; private set; }
    public bool SettingsImported { get; private set; }

    private bool _initialScrollLock = true;
    private List<AlarmItem> _alarms = new();
    private List<DefaultAvatar> _defaultAvatars = new();
    private readonly List<ImageRow> _idleRows = new();
    private readonly List<ImageRow> _playRows = new();
    private readonly List<(Button Button, FrameworkElement Target)> _tocButtons = new();
    private readonly DispatcherTimer _tocScrollTimer;
    private readonly DispatcherTimer _noticeTimer;
    private readonly System.Diagnostics.Stopwatch _tocScrollWatch = new();
    private double _tocScrollStart;
    private double _tocScrollTarget;
    private bool _tocExpanded;
    private bool _loading = true;
    private bool _dirty;
    private string _baselineFormState = "";

    public SettingsWindow(PetSettings settings)
    {
        InitializeComponent();
        Width = Math.Min(840, Math.Max(640, SystemParameters.WorkArea.Width - 40));
        Height = Math.Min(800, Math.Max(520, SystemParameters.WorkArea.Height - 40));
        Settings = settings.Clone();
        ReadingShowHotkeyBox.BindingChanged += OnReadingHotkeyBindingChanged;
        ReadingPrevHotkeyBox.BindingChanged += OnReadingHotkeyBindingChanged;
        ReadingNextHotkeyBox.BindingChanged += OnReadingHotkeyBindingChanged;
        SettingsScroll.AddHandler(TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(OnAnyTextChanged), true);
        SettingsScroll.AddHandler(ToggleButton.CheckedEvent,
            new RoutedEventHandler(OnAnyToggleChanged), true);
        SettingsScroll.AddHandler(ToggleButton.UncheckedEvent,
            new RoutedEventHandler(OnAnyToggleChanged), true);
        SettingsScroll.AddHandler(Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(OnAnySelectionChanged), true);
        SettingsScroll.AddHandler(RangeBase.ValueChangedEvent,
            new RoutedPropertyChangedEventHandler<double>(OnAnyRangeChanged), true);
        _tocScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _tocScrollTimer.Tick += OnTocScrollTick;
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            ShowNotice("");
        };
        BuildToc();
        SetTocExpanded(settings.SettingsTocExpanded, persist: false);
        ShowStatusCheck.IsChecked = settings.ShowStatusBar;
        GreetingCheck.IsChecked = settings.GreetingEnabled;
        AutoStartCheck.IsChecked = settings.StartWithWindows;
        ShowSettingsButtonCheck.IsChecked = settings.ShowSettingsButton;
        ShowCharacterPanelCheck.IsChecked = settings.ShowCharacterPanel;
        PanelHotkeyCtrlCheck.IsChecked = settings.PanelHotkeyCtrl;
        PanelHotkeyAltCheck.IsChecked = settings.PanelHotkeyAlt;
        PanelHotkeyShiftCheck.IsChecked = settings.PanelHotkeyShift;
        PanelHotkeyWinCheck.IsChecked = settings.PanelHotkeyWin;
        PanelHotkeyKeyBox.Text = settings.PanelHotkeyKey;
        PanelOpacitySlider.Value = Math.Clamp(settings.PanelOpacity, 0, 1) * 100;
        PanelScaleSlider.Value = Math.Clamp(settings.PanelScale, 0.5, 2);
        UpdatePanelValueLabels();
        FishingOverlayScaleSlider.Value = Math.Clamp(settings.FishingOverlayScale, 0.5, 2);
        CookingOverlayScaleSlider.Value = Math.Clamp(settings.CookingOverlayScale, 0.5, 2);
        MiniGameOpacitySlider.Value = Math.Clamp(settings.MiniGameOpacity, 0, 1) * 100;
        UpdateMiniGameValueLabels();
        ReadingEnabledCheck.IsChecked = settings.ReadingEnabled;
        ReadingTextPathBox.Text = settings.ReadingTextPath;
        ReadingWidthBox.Text = settings.ReadingWidth.ToString(CultureInfo.InvariantCulture);
        ReadingHeightBox.Text = settings.ReadingHeight.ToString(CultureInfo.InvariantCulture);
        ReadingBackgroundBox.Text = settings.ReadingBackground;
        ReadingBackgroundOpacityBox.Text = (settings.ReadingBackgroundOpacity * 100).ToString("0", CultureInfo.InvariantCulture);
        ReadingFontSizeBox.Text = settings.ReadingFontSize.ToString(CultureInfo.InvariantCulture);
        ReadingTextColorBox.Text = settings.ReadingTextColor;
        ReadingTextOpacityBox.Text = (settings.ReadingTextOpacity * 100).ToString("0", CultureInfo.InvariantCulture);
        ReadingFontFamilyBox.Text = settings.ReadingFontFamily;
        ReadingLineHeightBox.Text = settings.ReadingLineHeight.ToString("0.0", CultureInfo.InvariantCulture);
        ReadingCharsBox.Text = settings.ReadingCharsPerPage.ToString(CultureInfo.InvariantCulture);
        ReadingEncodingCombo.ItemsSource = PetSettings.ReadingEncodings;
        ReadingEncodingCombo.SelectedItem = settings.ReadingEncoding;
        ReadingShowHotkeyBox.Text = settings.ReadingShowHotkey;
        ReadingPrevHotkeyBox.Text = settings.ReadingPrevHotkey;
        ReadingNextHotkeyBox.Text = settings.ReadingNextHotkey;
        ReadingService.Reload(Settings);
        Settings.ReadingPageIndex = Math.Clamp(Settings.ReadingPageIndex, 1, ReadingService.PageCount);
        ReadingPageBox.Text = Settings.ReadingPageIndex.ToString(CultureInfo.InvariantCulture);
        UpdateReadingPageLabel();
        OrganizerEnabledCheck.IsChecked = settings.OrganizerEnabled;
        OrganizerWidthBox.Text = settings.OrganizerWidth.ToString(CultureInfo.InvariantCulture);
        OrganizerHeightBox.Text = settings.OrganizerHeight.ToString(CultureInfo.InvariantCulture);
        OrganizerBackgroundBox.Text = settings.OrganizerBackground;
        OrganizerOpacityBox.Text = (settings.OrganizerBackgroundOpacity * 100).ToString("0", CultureInfo.InvariantCulture);
        TodoEnabledCheck.IsChecked = settings.TodoEnabled;
        TodoWidthBox.Text = settings.TodoWidth.ToString(CultureInfo.InvariantCulture);
        TodoHeightBox.Text = settings.TodoHeight.ToString(CultureInfo.InvariantCulture);
        TodoBackgroundBox.Text = settings.TodoBackground;
        TodoOpacityBox.Text = (settings.TodoBackgroundOpacity * 100).ToString("0", CultureInfo.InvariantCulture);
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
        _defaultAvatars = GameRuntime.Engine?.DefaultAvatars
            .Select(avatar => new DefaultAvatar
            {
                Id = avatar.Id,
                Kind = avatar.Kind,
                Data = avatar.Data,
                Enabled = avatar.Enabled,
                SortIndex = avatar.SortIndex,
            })
            .ToList() ?? new List<DefaultAvatar>();
        if (_defaultAvatars.Count == 0)
        {
            _defaultAvatars.Add(new DefaultAvatar
            {
                Id = "builtin-default",
                Kind = "Builtin",
                Enabled = true,
                Data = "",
            });
        }
        PopulateImageRows(IdleImageRows, _idleRows, settings.IdleImages, settings.DisabledIdleImages);
        PopulateImageRows(PlayImageRows, _playRows, settings.PlayImages, settings.DisabledPlayImages);
        RefreshDefaultAvatarRows();
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
            _loading = false;
            _baselineFormState = CaptureFormState();
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

    private void MarkDirty()
    {
        if (_loading) return;
        _dirty = true;
    }

    private void OnAnyTextChanged(object sender, TextChangedEventArgs e) => MarkDirty();

    private void OnAnyToggleChanged(object sender, RoutedEventArgs e) => MarkDirty();

    private void OnAnySelectionChanged(object sender, SelectionChangedEventArgs e) => MarkDirty();

    private void OnAnyRangeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => MarkDirty();

    private bool HasUnsavedChanges()
    {
        if (_baselineFormState.Length == 0) return _dirty;
        return !string.Equals(_baselineFormState, CaptureFormState(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Captures the actual form values, not just a change flag. Programmatic
    /// restyling or focus changes therefore cannot produce a false warning.
    /// </summary>
    private string CaptureFormState()
    {
        var builder = new StringBuilder();
        void Add(string kind, string name, string value) =>
            builder.Append(kind).Append('\u001f').Append(name).Append('\u001f')
                .Append(value).Append('\u001e');

        foreach (var element in FindVisualChildren<FrameworkElement>(SettingsScroll))
        {
            switch (element)
            {
                case HotkeyRecorderBox recorder:
                    Add("hotkey", recorder.Name, recorder.Text);
                    break;
                case TextBox textBox:
                    Add("text", textBox.Name, textBox.Text);
                    break;
                case CheckBox checkBox:
                    Add("check", checkBox.Name, checkBox.IsChecked?.ToString() ?? "");
                    break;
                case RadioButton radio:
                    Add("radio", radio.Name, radio.IsChecked?.ToString() ?? "");
                    break;
                case ComboBox comboBox:
                    Add("combo", comboBox.Name,
                        comboBox.SelectedIndex.ToString(CultureInfo.InvariantCulture));
                    break;
                case Slider slider:
                    Add("slider", slider.Name, slider.Value.ToString("R", CultureInfo.InvariantCulture));
                    break;
            }
        }

        builder.Append("alarms\u001f")
            .Append(string.Join("\u001d", _alarms.Select(alarm =>
                $"{alarm.Enabled}|{alarm.Date}|{alarm.Start}|{alarm.End}|{alarm.Text}|{alarm.Popup}")))
            .Append('\u001e');
        builder.Append("avatars\u001f")
            .Append(string.Join("\u001d", _defaultAvatars.Select(avatar =>
                $"{avatar.Id}|{avatar.Kind}|{avatar.Enabled}|{avatar.SortIndex}")))
            .Append('\u001e');
        builder.Append("idle-rows\u001f")
            .Append(string.Join("\u001d", _idleRows.Select(row =>
                $"{row.Path.Text}|{row.Enabled.IsChecked}")))
            .Append('\u001e');
        builder.Append("play-rows\u001f")
            .Append(string.Join("\u001d", _playRows.Select(row =>
                $"{row.Path.Text}|{row.Enabled.IsChecked}")))
            .Append('\u001e');
        return builder.ToString();
    }

    private void ApplyAppearance()
    {
        var textBrush = Brush(Settings.SettingsTextColor);
        var delta = Settings.SettingsFontSize - 11;
        foreach (var text in FindVisualChildren<System.Windows.Controls.TextBlock>(this))
        {
            if (ReferenceEquals(text, ErrorText)) continue;
            if (IsInsideComboBox(text)) continue;
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
        if (!_initialScrollLock)
        {
            UpdateTocHighlight();
            return;
        }
        if (Math.Abs(SettingsScroll.VerticalOffset) > 0.5)
        {
            SettingsScroll.ScrollToTop();
        }
    }

    private void ShowNotice(string message)
    {
        _noticeTimer.Stop();
        ErrorText.Text = message;
        var visible = !string.IsNullOrWhiteSpace(message);
        NoticeBubble.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible) _noticeTimer.Start();
    }

    private void OnWeatherChanged(object sender, RoutedEventArgs e) => UpdateWeatherLocationState();

    private void OnPanelOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdatePanelValueLabels();

    private void OnPanelScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdatePanelValueLabels();

    private void OnFishingOverlayScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMiniGameValueLabels();
        PreviewMiniGameSettings();
    }
    private void OnCookingOverlayScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMiniGameValueLabels();
        PreviewMiniGameSettings();
    }

    private void OnMiniGameOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMiniGameValueLabels();
        PreviewMiniGameSettings();
    }

    private void UpdateMiniGameValueLabels()
    {
        if (FishingOverlayScaleValueText is not null)
        {
            FishingOverlayScaleValueText.Text = $"{FishingOverlayScaleSlider.Value:0.00}×";
        }
        if (CookingOverlayScaleValueText is not null)
        {
            CookingOverlayScaleValueText.Text = $"{CookingOverlayScaleSlider.Value:0.00}×";
        }
        if (MiniGameOpacityValueText is not null)
        {
            MiniGameOpacityValueText.Text = $"{MiniGameOpacitySlider.Value:0}%";
        }
    }

    private void PreviewMiniGameSettings()
    {
        if (FishingOverlayScaleSlider is null || CookingOverlayScaleSlider is null ||
            MiniGameOpacitySlider is null || !IsLoaded)
        {
            return;
        }
        Settings.FishingOverlayScale = Math.Clamp(FishingOverlayScaleSlider.Value, 0.5, 2);
        Settings.CookingOverlayScale = Math.Clamp(CookingOverlayScaleSlider.Value, 0.5, 2);
        Settings.MiniGameOpacity = Math.Clamp(MiniGameOpacitySlider.Value / 100.0, 0, 1);
        PreviewChanged?.Invoke(Settings);
    }

    private void UpdatePanelValueLabels()
    {
        if (PanelOpacityValueText is not null)
        {
            PanelOpacityValueText.Text = $"{PanelOpacitySlider.Value:0}%";
        }
        if (PanelScaleValueText is not null)
        {
            PanelScaleValueText.Text = $"{PanelScaleSlider.Value:0.00}×";
        }
    }

    private void OnReadingHotkeyBindingChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded) return;
        MarkDirty();
        if (!TryReadReadingFields(out _)) return;
        if (!ValidateReadingHotkeyConflicts(CurrentPanelHotkeyText())) return;
        PreviewReadingSettings();
    }

    private string CurrentPanelHotkeyText() => HotkeyGesture.Normalize(
        $"{(PanelHotkeyCtrlCheck.IsChecked == true ? "Ctrl+" : "")}" +
        $"{(PanelHotkeyAltCheck.IsChecked == true ? "Alt+" : "")}" +
        $"{(PanelHotkeyShiftCheck.IsChecked == true ? "Shift+" : "")}" +
        $"{(PanelHotkeyWinCheck.IsChecked == true ? "Win+" : "")}{PanelHotkeyKeyBox.Text}",
        "Ctrl+Alt+P");

    private void OnReadingChanged(object sender, RoutedEventArgs e)
    {
        PreviewReadingSettings();
    }

    private void OnReadingFieldKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        e.Handled = true;
        if (ReferenceEquals(sender, ReadingCharsBox)) OnReadingCharsCommit(sender, new RoutedEventArgs());
        else OnReadingPageCommit(sender, new RoutedEventArgs());
    }

    private void OnReadingCharsCommit(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!int.TryParse(ReadingCharsBox.Text.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var chars) || chars is < 1 or > 1500)
        {
            ReadingCharsBox.Text = Settings.ReadingCharsPerPage.ToString(CultureInfo.InvariantCulture);
            ShowNotice("每页文字数量必须是 1–1500。");
            return;
        }
        if (chars == Settings.ReadingCharsPerPage) return;

        var oldChars = Math.Clamp(Settings.ReadingCharsPerPage, 1, 1500);
        var oldPage = Math.Max(1, Settings.ReadingPageIndex);
        var offset = Math.Max(0, oldPage - 1) * oldChars;
        Settings.ReadingCharsPerPage = chars;
        ReadingService.Reload(Settings);
        var total = Math.Max(1, ReadingService.PageCount);
        Settings.ReadingPageIndex = Math.Clamp(offset / chars + 1, 1, total);
        ReadingPageBox.Text = Settings.ReadingPageIndex.ToString(CultureInfo.InvariantCulture);
        PreviewReadingSettings();
        UpdateReadingPageLabel();
    }

    private void OnReadingPageCommit(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var total = Math.Max(1, ReadingService.PageCount);
        if (!int.TryParse(ReadingPageBox.Text.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var page))
        {
            page = Settings.ReadingPageIndex;
        }
        var corrected = Math.Clamp(page, 1, total);
        if (corrected != page)
        {
            ShowNotice($"页码超出范围，已修正到第 {corrected} 页。");
        }
        Settings.ReadingPageIndex = corrected;
        ReadingPageBox.Text = corrected.ToString(CultureInfo.InvariantCulture);
        UpdateReadingPageLabel();
        PreviewReadingSettings();
    }

    private void OnResetReadingPosition(object sender, RoutedEventArgs e)
    {
        var position = ResetReadingPositionRequested?.Invoke();
        if (position is { } p)
        {
            Settings.ReadingLeft = p.Left;
            Settings.ReadingTop = p.Top;
        }
        else
        {
            Settings.ReadingLeft = null;
            Settings.ReadingTop = null;
        }
        ShowNotice("阅读区域已重置到主屏幕工作区。");
    }

    private void UpdateReadingPageLabel()
    {
        if (ReadingPageLabel is null) return;
        var total = Math.Max(1, ReadingService.PageCount);
        var page = Math.Clamp(Settings.ReadingPageIndex, 1, total);
        ReadingPageLabel.Text = $"当前页数（{page} / {total}）";
    }

    private void OnBrowseReadingText(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择阅读文本",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;
        ReadingTextPathBox.Text = dialog.FileName;
    }

    private void OnReloadReadingText(object sender, RoutedEventArgs e)
    {
        MarkDirty();
        if (!TryReadReadingFields(out _))
        {
            ShowNotice("阅读设置填写有误，请检查宽度、高度、透明度、字号、行间距或每页字数。");
            return;
        }
        if (!ValidateReadingHotkeyConflicts(CurrentPanelHotkeyText())) return;
        ReadingService.Reload(Settings);
        Settings.ReadingPageIndex = 1;
        ReadingPageBox.Text = "1";
        UpdateReadingPageLabel();
        ReadingPreviewChanged?.Invoke(Settings.Clone());
        if (ReadingService.LastError.Length > 0)
        {
            ShowNotice(ReadingService.LastError);
        }
    }

    private bool TryReadReadingFields(out PetSettings output)
    {
        output = Settings;
        if (!double.TryParse(ReadingWidthBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
            width is < 30 or > 1200 ||
            !double.TryParse(ReadingHeightBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var height) ||
            height is < 30 or > 800 ||
            !double.TryParse(ReadingBackgroundOpacityBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bgOpacity) ||
            bgOpacity is < 0 or > 100 ||
            !double.TryParse(ReadingFontSizeBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize) ||
            fontSize is < 8 or > 40 ||
            !double.TryParse(ReadingTextOpacityBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var textOpacity) ||
            textOpacity is < 0 or > 100 ||
            !double.TryParse(ReadingLineHeightBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lineHeight) ||
            lineHeight is < 1.0 or > 2.0 ||
            !int.TryParse(ReadingCharsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var chars) ||
            chars is < 1 or > 1500)
        {
            return false;
        }
        if (!TryColor(ReadingBackgroundBox.Text, out var background) ||
            !TryColor(ReadingTextColorBox.Text, out var textColor))
        {
            return false;
        }
        if (!IsHotkeyTextValid(ReadingShowHotkeyBox.Text) ||
            !IsHotkeyTextValid(ReadingPrevHotkeyBox.Text) ||
            !IsHotkeyTextValid(ReadingNextHotkeyBox.Text))
        {
            return false;
        }

        Settings.ReadingEnabled = ReadingEnabledCheck.IsChecked == true;
        Settings.ReadingTextPath = ReadingTextPathBox.Text.Trim();
        Settings.ReadingWidth = width;
        Settings.ReadingHeight = height;
        Settings.ReadingBackground = background;
        Settings.ReadingBackgroundOpacity = bgOpacity / 100.0;
        Settings.ReadingFontSize = fontSize;
        Settings.ReadingTextColor = textColor;
        Settings.ReadingTextOpacity = textOpacity / 100.0;
        Settings.ReadingFontFamily = ReadingFontFamilyBox.Text.Trim();
        Settings.ReadingLineHeight = lineHeight;
        Settings.ReadingCharsPerPage = chars;
        Settings.ReadingEncoding = ReadingEncodingCombo.SelectedItem as string ?? "自动检测";
        Settings.ReadingShowHotkey = NormalizeReadingHotkey(ReadingShowHotkeyBox.Text, "Ctrl+Alt+M");
        Settings.ReadingPrevHotkey = NormalizeReadingHotkey(ReadingPrevHotkeyBox.Text, "Ctrl+,");
        Settings.ReadingNextHotkey = NormalizeReadingHotkey(ReadingNextHotkeyBox.Text, "Ctrl+.");
        return true;
    }

    private void PreviewReadingSettings()
    {
        if (!IsLoaded || ReadingWidthBox is null) return;
        if (!TryReadReadingFields(out _)) return;
        if (!ValidateReadingHotkeyConflicts(CurrentPanelHotkeyText())) return;
        ReadingService.Reload(Settings);
        var total = Math.Max(1, ReadingService.PageCount);
        if (Settings.ReadingPageIndex > total)
        {
            Settings.ReadingPageIndex = total;
            ReadingPageBox.Text = total.ToString(CultureInfo.InvariantCulture);
        }
        UpdateReadingPageLabel();
        ReadingPreviewChanged?.Invoke(Settings.Clone());
    }

    private static bool SameGesture(HotkeyGesture left, HotkeyGesture right) =>
        left.Modifiers == right.Modifiers && left.VirtualKey == right.VirtualKey;

    private static bool IsHotkeyTextValid(string? text) =>
        string.IsNullOrWhiteSpace(text) || HotkeyGesture.TryParse(text, out _);

    private static string NormalizeReadingHotkey(string? text, string fallback) =>
        string.IsNullOrWhiteSpace(text) ? "" : HotkeyGesture.Normalize(text, fallback);

    private bool ValidateReadingHotkeyConflicts(string panelText)
    {
        var existing = new List<(string Name, HotkeyGesture Gesture)>();
        if (HotkeyGesture.TryParse(panelText, out var panelGesture))
            existing.Add(("角色面板", panelGesture));
        if (HotkeyGesture.TryParse("Ctrl+Alt+G", out var avatarGesture))
            existing.Add(("切换形象", avatarGesture));
        var screenshotText = HotkeyGesture.Normalize(
            $"{(ScreenshotCtrlCheck.IsChecked == true ? "Ctrl+" : "")}" +
            $"{(ScreenshotAltCheck.IsChecked == true ? "Alt+" : "")}" +
            $"{(ScreenshotShiftCheck.IsChecked == true ? "Shift+" : "")}" +
            $"{(ScreenshotWinCheck.IsChecked == true ? "Win+" : "")}{ScreenshotKeyBox.Text}",
            "Ctrl+Alt+A");
        if (HotkeyGesture.TryParse(screenshotText, out var screenshotGesture))
            existing.Add(("截图", screenshotGesture));

        var reading = new (string Name, string Text, HotkeyRecorderBox Box)[]
        {
            ("显示/隐藏", Settings.ReadingShowHotkey, ReadingShowHotkeyBox),
            ("上一页", Settings.ReadingPrevHotkey, ReadingPrevHotkeyBox),
            ("下一页", Settings.ReadingNextHotkey, ReadingNextHotkeyBox),
        };
        foreach (var (name, text, box) in reading)
        {
            if (!HotkeyGesture.TryParse(text, out var gesture)) continue;
            foreach (var other in existing)
            {
                if (SameGesture(gesture, other.Gesture))
                {
                    ClearReadingHotkey(name, box);
                    ShowNotice("快捷键已被占用，请重新设置");
                    return false;
                }
            }
            existing.Add((name, gesture));
        }
        return true;
    }

    private void ClearReadingHotkey(string name, HotkeyRecorderBox box)
    {
        box.Text = "";
        switch (name)
        {
            case "显示/隐藏":
                Settings.ReadingShowHotkey = "";
                break;
            case "上一页":
                Settings.ReadingPrevHotkey = "";
                break;
            case "下一页":
                Settings.ReadingNextHotkey = "";
                break;
        }
    }

    private void BuildToc()
    {
        if (TocItemsPanel is null) return;
        TocItemsPanel.Children.Clear();
        _tocButtons.Clear();
        var entries = new (string Label, FrameworkElement Target)[]
        {
            ("基础", BasicSectionTitle),
            ("角色面板设置", PanelSettingsSectionTitle),
            ("摸鱼游戏设置", MiniGameSectionTitle),
            ("阅读设置", ReadingSectionTitle),
            ("迁移数据", MigrationSectionTitle),
            ("卸载桌宠", UninstallSectionTitle),
            ("外观", AppearanceSectionTitle),
            ("截图", ScreenshotSectionTitle),
            ("天气位置", WeatherSectionTitle),
            ("工作日程", WorkSectionTitle),
            ("提醒与工具", ReminderSectionTitle),
            ("图标收纳区设置", OrganizerSectionTitle),
            ("桌面待办区设置", TodoSectionTitle),
            ("角色形象", CharacterImageSectionTitle),
            ("互动内容", InteractionSectionTitle),
        };
        foreach (var (label, target) in entries)
        {
            var button = new Button
            {
                Content = label,
                Height = 28,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(9, 0, 6, 0),
                Margin = new Thickness(0, 0, 0, 3),
                FontSize = 10,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xB6, 0xCE)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            button.Click += (_, _) => ScrollToSection(target);
            TocItemsPanel.Children.Add(button);
            _tocButtons.Add((button, target));
        }
        UpdateTocHighlight();
    }

    private void OnToggleToc(object sender, RoutedEventArgs e) =>
        SetTocExpanded(!_tocExpanded, persist: true);

    private void SetTocExpanded(bool expanded, bool persist)
    {
        _tocExpanded = expanded;
        TocColumn.Width = new GridLength(expanded ? 180 : 0);
        TocPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        TocButton.Content = expanded ? "目录 ◂" : "目录";
        if (persist)
        {
            Settings.SettingsTocExpanded = expanded;
            SettingsStore.Save(Settings);
        }
        if (expanded) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateTocHighlight));
    }

    private void ScrollToSection(FrameworkElement target)
    {
        if (target is null || SettingsContent is null) return;
        if (HasUnsavedChanges())
        {
            if (!ConfirmDialog.Show(this, "当前有未保存的设置，是否放弃修改？",
                    "未保存的设置", "放弃修改", Settings.ThemeColor))
            {
                return;
            }
            _baselineFormState = CaptureFormState();
            _dirty = false;
        }
        try
        {
            var point = target.TransformToAncestor(SettingsContent).Transform(new Point(0, 0));
            _tocScrollStart = SettingsScroll.VerticalOffset;
            _tocScrollTarget = Math.Clamp(point.Y - 8, 0,
                Math.Max(0, SettingsScroll.ScrollableHeight));
            _tocScrollWatch.Restart();
            _tocScrollTimer.Start();
        }
        catch
        {
            // The section may not be laid out yet.
        }
    }

    private void OnTocScrollTick(object? sender, EventArgs e)
    {
        var progress = Math.Clamp(_tocScrollWatch.Elapsed.TotalMilliseconds / 260.0, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        SettingsScroll.ScrollToVerticalOffset(
            _tocScrollStart + (_tocScrollTarget - _tocScrollStart) * eased);
        if (progress < 1) return;
        _tocScrollTimer.Stop();
        UpdateTocHighlight();
    }

    private void UpdateTocHighlight()
    {
        if (_tocButtons.Count == 0 || SettingsScroll is null) return;
        var currentY = SettingsScroll.VerticalOffset + 36;
        (Button Button, FrameworkElement Target)? active = null;
        foreach (var entry in _tocButtons)
        {
            try
            {
                var y = entry.Target.TransformToAncestor(SettingsContent)
                    .Transform(new Point(0, 0)).Y;
                if (y <= currentY) active = entry;
            }
            catch
            {
                // Ignore sections not yet in the visual tree.
            }
        }
        if (active is null && _tocButtons.Count > 0) active = _tocButtons[0];
        foreach (var entry in _tocButtons)
        {
            var selected = active is not null && ReferenceEquals(entry.Button, active.Value.Button);
            entry.Button.Background = selected
                ? new SolidColorBrush(Color.FromArgb(0x2E, 0x8F, 0xD3, 0xFF))
                : Brushes.Transparent;
            entry.Button.Foreground = new SolidColorBrush(selected
                ? Color.FromRgb(0xFF, 0xFF, 0xFF)
                : Color.FromRgb(0x9A, 0xB6, 0xCE));
            entry.Button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void OnExportMigration(object sender, RoutedEventArgs e)    {
        if (GameRuntime.Engine is not { } engine)
        {
            ShowNotice("游戏数据未初始化，无法导出迁移包。");
            return;
        }
        try
        {
            new MigrationService(engine).Export(this);
        }
        catch (Exception error)
        {
            ShowNotice("导出失败：" + error.Message);
        }
    }

    private void OnImportMigration(object sender, RoutedEventArgs e)
    {
        if (GameRuntime.Engine is not { } engine)
        {
            ShowNotice("游戏数据未初始化，无法导入迁移包。");
            return;
        }
        try
        {
            new MigrationService(engine).Import(this);
            SettingsImported = true;
            if (Owner is MainWindow main) main.ReloadSettingsFromDisk();
            ConfirmDialog.ShowInfo(this, "迁移包导入成功，设置已重新加载。", "导入成功",
                Settings.ThemeColor);
            Close();
        }
        catch (Exception error)
        {
            ShowNotice("导入失败：" + error.Message);
        }
    }

    private void OnActivationMigration(object sender, RoutedEventArgs e)
    {
        new ActivationWindow { Owner = this }.ShowDialog();
    }

    private void OnUninstallPet(object sender, RoutedEventArgs e)
    {
        if (GameRuntime.Engine is null) return;
        var confirm = new UninstallWindow { Owner = this };
        if (confirm.ShowDialog() != true) return;
        try
        {
            if (!confirm.ClearUserData)
            {
                GameRuntime.Shutdown(save: true);
                Application.Current.Shutdown();
                return;
            }

            PersistentStore.SuppressWrites = true;
            if (Owner is MainWindow main) main.SuppressSaveOnShutdown = true;
            GameRuntime.Shutdown(save: false);
            UserDataCleaner.DeleteAll();
            Application.Current.Shutdown();
        }
        catch (Exception error)
        {
            ShowNotice("清除失败：" + error.Message);
        }
    }

    private void OnEditAlarms(object sender, RoutedEventArgs e)
    {
        var dialog = new AlarmWindow(_alarms) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _alarms = dialog.Alarms;
            MarkDirty();
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

    private void OnAddIdleImage(object sender, RoutedEventArgs e) => AddImageFromDialog(IdleImageRows, _idleRows);

    private void OnAddPlayImage(object sender, RoutedEventArgs e) => AddImageFromDialog(PlayImageRows, _playRows);

    private static bool IsInsideComboBox(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is ComboBox) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void RefreshDefaultAvatarRows()
    {
        if (DefaultAvatarRows is null) return;
        DefaultAvatarRows.Children.Clear();
        var customIndex = 0;
        foreach (var avatar in _defaultAvatars.ToList())
        {
            if (!avatar.IsBuiltIn) customIndex++;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var preview = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x22, 0x3A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x5B, 0x84)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new Image
                {
                    Source = LoadDefaultAvatarImage(avatar),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(3),
                },
            };
            var text = new TextBlock
            {
                Text = avatar.IsBuiltIn ? "内置默认主形象（不可删除）" : $"自定义主形象 {customIndex}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xCF, 0xEA, 0xFF)),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var enabled = new CheckBox
            {
                IsChecked = avatar.IsBuiltIn || avatar.Enabled,
                IsEnabled = !avatar.IsBuiltIn,
                Content = "启用",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
            };
            enabled.Click += (_, _) =>
            {
                avatar.Enabled = enabled.IsChecked == true;
            };
            var remove = new Button
            {
                Content = "×",
                Style = (Style)FindResource("Action"),
                Width = 28,
                Height = 26,
                Padding = new Thickness(0),
                IsEnabled = !avatar.IsBuiltIn,
                ToolTip = "删除该自定义主形象",
            };
            remove.Click += (_, _) =>
            {
                if (avatar.IsBuiltIn) return;
                _defaultAvatars.Remove(avatar);
                MarkDirty();
                RefreshDefaultAvatarRows();
            };
            Grid.SetColumn(preview, 0);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(enabled, 2);
            Grid.SetColumn(remove, 3);
            row.Children.Add(preview);
            row.Children.Add(text);
            row.Children.Add(enabled);
            row.Children.Add(remove);
            DefaultAvatarRows.Children.Add(row);
        }
        if (AddDefaultAvatarButton is not null)
        {
            AddDefaultAvatarButton.IsEnabled = _defaultAvatars.Count(avatar => !avatar.IsBuiltIn) < 5;
        }
    }

    private void OnAddDefaultAvatar(object sender, RoutedEventArgs e)
    {
        if (_defaultAvatars.Count(avatar => !avatar.IsBuiltIn) >= 5)
        {
            ShowNotice("自定义主形象最多 5 张。");
            return;
        }
        var dialog = new OpenFileDialog
        {
            Title = "选择默认主形象图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|所有文件|*.*",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bytes = File.ReadAllBytes(dialog.FileName);
            if (bytes.Length is 0 or > 8 * 1024 * 1024)
            {
                ShowNotice("图片为空或超过 8 MB。");
                return;
            }
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) throw new InvalidOperationException("图片无法解码。");
            _defaultAvatars.Add(new DefaultAvatar
            {
                Id = "custom-" + Guid.NewGuid().ToString("N"),
                Kind = "Custom",
                Data = Convert.ToBase64String(bytes),
                Enabled = true,
            });
            MarkDirty();
            RefreshDefaultAvatarRows();
        }
        catch (Exception error)
        {
            ShowNotice("图片读取失败：" + error.Message);
        }
    }

    private static BitmapSource? LoadDefaultAvatarImage(DefaultAvatar avatar)
    {
        try
        {
            byte[]? bytes = null;
            if (!string.IsNullOrWhiteSpace(avatar.Data))
            {
                bytes = Convert.FromBase64String(avatar.Data);
            }
            else if (avatar.IsBuiltIn)
            {
                var path = AssetBootstrapper.PathOf("main.png");
                if (File.Exists(path)) bytes = File.ReadAllBytes(path);
            }
            if (bytes is null || bytes.Length == 0) return null;
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void PopulateImageRows(StackPanel panel, List<ImageRow> rows,
        IEnumerable<string> images, IEnumerable<string> disabled)
    {
        panel.Children.Clear();
        rows.Clear();
        var disabledSet = new HashSet<string>(disabled ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var path in images ?? Enumerable.Empty<string>())
        {
            var text = (path ?? "").Trim();
            if (text.Length == 0) continue;
            AddImageRow(panel, rows, text, !disabledSet.Contains(text));
        }
    }

    private void AddImageRow(StackPanel panel, List<ImageRow> rows, string path, bool enabled)
    {
        if (rows.Count >= 24) return;

        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox
        {
            IsChecked = enabled,
            ToolTip = "启用该图片",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        System.Windows.Automation.AutomationProperties.SetName(checkBox, "启用图片 " + path);
        checkBox.ToolTip = "启用该图片\n" + path;
        var pathBox = new TextBox
        {
            Text = path,
            Height = 28,
            FontSize = 10,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        pathBox.TextChanged += (_, _) =>
        {
            var current = pathBox.Text.Trim();
            System.Windows.Automation.AutomationProperties.SetName(
                checkBox,
                current.Length == 0 ? "启用图片" : "启用图片 " + current);
            checkBox.ToolTip = current.Length == 0 ? "启用该图片" : "启用该图片\n" + current;
        };
        var browse = new Button
        {
            Content = "浏览…",
            Style = (Style)FindResource("Action"),
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(8, 3, 8, 3),
            FontSize = 10,
        };
        var remove = new Button
        {
            Content = "×",
            Style = (Style)FindResource("Action"),
            Padding = new Thickness(9, 3, 9, 3),
            FontSize = 10,
            ToolTip = "删除这张图片",
        };

        var row = new ImageRow(grid, checkBox, pathBox);
        Grid.SetColumn(pathBox, 1);
        Grid.SetColumn(browse, 2);
        Grid.SetColumn(remove, 3);
        grid.Children.Add(checkBox);
        grid.Children.Add(pathBox);
        grid.Children.Add(browse);
        grid.Children.Add(remove);

        browse.Click += (_, _) => BrowseImageRow(rows, row);
        remove.Click += (_, _) =>
        {
            rows.Remove(row);
            panel.Children.Remove(grid);
            MarkDirty();
        };

        rows.Add(row);
        panel.Children.Add(grid);
    }

    private void AddImageFromDialog(StackPanel panel, List<ImageRow> rows)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择角色图片",
            Filter = "图片文件|*.png;*.webp;*.jpg;*.jpeg;*.gif;*.bmp|所有文件|*.*",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true) return;
        AddImageRow(panel, rows, dialog.FileName, true);
        MarkDirty();
    }

    private void BrowseImageRow(List<ImageRow> rows, ImageRow row)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择角色图片",
            Filter = "图片文件|*.png;*.webp;*.jpg;*.jpeg;*.gif;*.bmp|所有文件|*.*",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true) return;
        row.Path.Text = dialog.FileName;
        row.Enabled.IsChecked = true;
    }

    private static List<string> GetImagePaths(List<ImageRow> rows)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var path = row.Path.Text.Trim();
            if (path.Length == 0 || !seen.Add(path)) continue;
            result.Add(path);
            if (result.Count >= 24) break;
        }
        return result;
    }

    private static List<string> GetDisabledImagePaths(List<ImageRow> rows)
    {
        return rows
            .Where(row => row.Enabled.IsChecked != true && !string.IsNullOrWhiteSpace(row.Path.Text))
            .Select(row => row.Path.Text.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ShowNotice("");
        if (IsLoaded)
        {
            OnReadingCharsCommit(this, new RoutedEventArgs());
            OnReadingPageCommit(this, new RoutedEventArgs());
        }
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
        var panelHotkeyKey = (PanelHotkeyKeyBox.Text ?? "").Trim().ToUpperInvariant();
        if (!PetSettings.ScreenshotKeys.Contains(panelHotkeyKey))
        {
            ShowNotice("角色面板快捷键的按键无效，请填 A–Z、0–9 或 F1–F12。");
            return;
        }
        if (PanelHotkeyCtrlCheck.IsChecked != true && PanelHotkeyAltCheck.IsChecked != true &&
            PanelHotkeyShiftCheck.IsChecked != true && PanelHotkeyWinCheck.IsChecked != true)
        {
            ShowNotice("角色面板快捷键至少需要 Ctrl、Alt、Shift、Win 中的一个修饰键。");
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

        var idle = GetImagePaths(_idleRows);
        var play = GetImagePaths(_playRows);
        var phrases = SplitLines(PhrasesBox.Text);
        var chatPhrases = SplitLines(ChatPhrasesBox.Text);
        var idleEnabled = _idleRows.Count(row => !string.IsNullOrWhiteSpace(row.Path.Text) && row.Enabled.IsChecked == true);
        var playEnabled = _playRows.Count(row => !string.IsNullOrWhiteSpace(row.Path.Text) && row.Enabled.IsChecked == true);
        if (idleEnabled < 1 || playEnabled < 1)
        {
            ShowNotice("空闲形象、互动形象都至少需要 1 张已启用图片。");
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
        Settings.ShowCharacterPanel = ShowCharacterPanelCheck.IsChecked == true;
        Settings.PanelHotkeyCtrl = PanelHotkeyCtrlCheck.IsChecked == true;
        Settings.PanelHotkeyAlt = PanelHotkeyAltCheck.IsChecked == true;
        Settings.PanelHotkeyShift = PanelHotkeyShiftCheck.IsChecked == true;
        Settings.PanelHotkeyWin = PanelHotkeyWinCheck.IsChecked == true;
        Settings.PanelHotkeyKey = panelHotkeyKey;
        Settings.PanelOpacity = Math.Clamp(PanelOpacitySlider.Value / 100.0, 0, 1);
        Settings.PanelScale = Math.Clamp(PanelScaleSlider.Value, 0.5, 2);
        Settings.FishingOverlayScale = Math.Clamp(FishingOverlayScaleSlider.Value, 0.5, 2);
        Settings.CookingOverlayScale = Math.Clamp(CookingOverlayScaleSlider.Value, 0.5, 2);
        Settings.MiniGameOpacity = Math.Clamp(MiniGameOpacitySlider.Value / 100.0, 0, 1);
        if (!TryReadReadingFields(out _))
        {
            ShowNotice("阅读设置填写有误，请检查尺寸、颜色、透明度、字号、行间距、每页字数或快捷键。");
            return;
        }
        var panelText = CurrentPanelHotkeyText();
        if (!ValidateReadingHotkeyConflicts(panelText))
        {
            return;
        }
        ReadingService.Reload(Settings);
        var totalReadingPages = Math.Max(1, ReadingService.PageCount);
        if (Settings.ReadingPageIndex > totalReadingPages)
        {
            Settings.ReadingPageIndex = totalReadingPages;
            ReadingPageBox.Text = totalReadingPages.ToString(CultureInfo.InvariantCulture);
            UpdateReadingPageLabel();
            ShowNotice($"页码超出范围，已修正到第 {totalReadingPages} 页。");
        }
        if (!double.TryParse(OrganizerWidthBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var organizerWidth) ||
            organizerWidth is < 300 or > 1200 ||
            !double.TryParse(OrganizerHeightBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var organizerHeight) ||
            organizerHeight is < 300 or > 1000 ||
            !double.TryParse(OrganizerOpacityBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var organizerOpacity) ||
            organizerOpacity is < 0 or > 100 ||
            !double.TryParse(TodoWidthBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var todoWidth) ||
            todoWidth is < 300 or > 1200 ||
            !double.TryParse(TodoHeightBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var todoHeight) ||
            todoHeight is < 300 or > 1000 ||
            !double.TryParse(TodoOpacityBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var todoOpacity) ||
            todoOpacity is < 0 or > 100)
        {
            ShowNotice("收纳区/待办区的大小必须在 300–1200 × 300–1000，透明度必须在 0–100。");
            return;
        }
        if (!TryColor(OrganizerBackgroundBox.Text, out var organizerBackground) ||
            !TryColor(TodoBackgroundBox.Text, out var todoBackground))
        {
            ShowNotice("收纳区/待办区背景颜色请使用 #RRGGBB 格式。");
            return;
        }
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
        Settings.MainImages = Settings.MainImages.Count > 0
            ? Settings.MainImages.ToList()
            : new List<string> { AssetBootstrapper.PathOf("main.png") };
        Settings.IdleImages = idle;
        Settings.PlayImages = play;
        Settings.DisabledMainImages = Settings.DisabledMainImages.ToList();
        Settings.DisabledIdleImages = GetDisabledImagePaths(_idleRows);
        Settings.DisabledPlayImages = GetDisabledImagePaths(_playRows);
        Settings.OrganizerEnabled = OrganizerEnabledCheck.IsChecked == true;
        Settings.OrganizerWidth = organizerWidth;
        Settings.OrganizerHeight = organizerHeight;
        Settings.OrganizerBackground = organizerBackground;
        Settings.OrganizerBackgroundOpacity = organizerOpacity / 100.0;
        Settings.TodoEnabled = TodoEnabledCheck.IsChecked == true;
        Settings.TodoWidth = todoWidth;
        Settings.TodoHeight = todoHeight;
        Settings.TodoBackground = todoBackground;
        Settings.TodoBackgroundOpacity = todoOpacity / 100.0;
        Settings.InteractionPhrases = phrases;
        Settings.ChatPhrases = chatPhrases.Count > 0 ? chatPhrases : PetSettings.CreateDefault().ChatPhrases;
        Settings.Normalize();
        GameRuntime.Engine?.ReplaceDefaultAvatars(_defaultAvatars);
        HasSavedSettings = true;
        SettingsSaved?.Invoke(Settings.Clone());
        _dirty = false;
        _baselineFormState = CaptureFormState();
        ShowNotice("保存成功");
    }

    private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;

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

    private sealed class ImageRow
    {
        public ImageRow(Grid container, CheckBox enabled, TextBox path)
        {
            Container = container;
            Enabled = enabled;
            Path = path;
        }

        public Grid Container { get; }
        public CheckBox Enabled { get; }
        public TextBox Path { get; }
    }
}

