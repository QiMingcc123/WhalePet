using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WhalePet.Game;

public partial class CharacterPanelWindow : Window
{
    private const double BaseWidth = 1080;
    private const double BaseHeight = 740;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private GameEngine? _engine;
    private PetSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _noticeTimer;
    private readonly Dictionary<string, WrapPanel> _achievementPanels = new();
    private readonly List<(Border Border, TextBlock Glyph)> _weeklyCells = new();
    private readonly Dictionary<string, BitmapSource> _gifThumbs = new(StringComparer.OrdinalIgnoreCase);
    private string _avatarKey = "";
    private string _gifSignature = "";
    private string _inventorySignature = "";
    private bool _refreshing;
    private bool _closed;
    private bool _positionInitialized;
    private bool _narrowLayout;

    public event Action<double, double>? PositionChanged;

    public CharacterPanelWindow(PetSettings settings)
    {
        InitializeComponent();
        _settings = settings.Clone();
        _engine = GameRuntime.Engine;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            RefreshLiveStats();
            if (_engine is not null && !_refreshing && !_closed)
            {
                RefreshTask();
                RefreshInventory();
            }
        };
        _noticeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            FooterNoticeText.Text = "";
        };

        BuildWeeklyCells();
        BuildAchievementTabs();
        ApplyDisplaySettings(settings);
        MainTabs.SizeChanged += (_, _) => UpdateWrapWidths();
        ResponsiveGrid.SizeChanged += (_, _) =>
        {
            UpdateResponsiveLayout();
            UpdateWrapWidths();
        };

        if (_engine is not null)
        {
            _engine.Changed += OnEngineChanged;
            _engine.AvatarChanged += OnAvatarChanged;
            _engine.Toast += OnEngineToast;
        }
        else
        {
            HeaderHintText.Text = GameRuntime.Error ?? "游戏数据未能初始化";
        }
    }

    public void ApplyDisplaySettings(PetSettings settings)
    {
        _settings = settings.Clone();
        var opacity = Math.Clamp(settings.PanelOpacity, 0, 1);
        Opacity = opacity;
        IsHitTestVisible = opacity > 0.001;
        SetClickThrough(opacity <= 0.001);

        var scale = Math.Clamp(settings.PanelScale, 0.5, 2);
        var area = SystemParameters.WorkArea;
        var width = Math.Clamp(BaseWidth * scale, 400, Math.Max(400, area.Width - 16));
        var height = Math.Clamp(BaseHeight * scale, 300, Math.Max(300, area.Height - 16));
        var previousWidth = Width;
        var previousHeight = Height;

        if (!_positionInitialized)
        {
            Width = width;
            Height = height;
            if (settings.PanelLeft is { } savedLeft && settings.PanelTop is { } savedTop &&
                double.IsFinite(savedLeft) && double.IsFinite(savedTop))
            {
                Left = savedLeft;
                Top = savedTop;
            }
            else
            {
                Left = area.Left + (area.Width - width) / 2;
                Top = area.Top + (area.Height - height) / 2;
            }
            _positionInitialized = true;
        }
        else
        {
            var centerX = Left + previousWidth / 2;
            var centerY = Top + previousHeight / 2;
            Width = width;
            Height = height;
            Left = centerX - width / 2;
            Top = centerY - height / 2;
        }

        BodyScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ClampToWorkArea();
        UpdateResponsiveLayout();
        UpdateWrapWidths();
        SavePosition();
    }

    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        var maxLeft = Math.Max(area.Left, area.Right - Width);
        var maxTop = Math.Max(area.Top, area.Bottom - Height);
        if (!double.IsFinite(Left)) Left = area.Left;
        if (!double.IsFinite(Top)) Top = area.Top;
        Left = Math.Clamp(Left, area.Left, maxLeft);
        Top = Math.Clamp(Top, area.Top, maxTop);
    }

    private void SavePosition()
    {
        if (!_positionInitialized || !IsLoaded) return;
        if (!double.IsFinite(Left) || !double.IsFinite(Top)) return;
        PositionChanged?.Invoke(Left, Top);
    }

    private void UpdateResponsiveLayout()
    {
        var width = ResponsiveGrid.ActualWidth;
        if (width <= 0) return;
        var narrow = width < 900;
        if (narrow == _narrowLayout) return;
        _narrowLayout = narrow;

        if (narrow)
        {
            LeftCard.Height = 480;
            AvatarRow.Height = new GridLength(190);
            Grid.SetRow(LeftCard, 0);
            Grid.SetColumn(LeftCard, 0);
            Grid.SetColumnSpan(LeftCard, 3);
            Grid.SetRow(MainTabs, 1);
            Grid.SetColumn(MainTabs, 0);
            Grid.SetColumnSpan(MainTabs, 3);
            MainTabs.Margin = new Thickness(0, 12, 0, 0);
        }
        else
        {
            LeftCard.Height = 600;
            AvatarRow.Height = new GridLength(292);
            Grid.SetRow(LeftCard, 0);
            Grid.SetColumn(LeftCard, 0);
            Grid.SetColumnSpan(LeftCard, 1);
            Grid.SetRow(MainTabs, 0);
            Grid.SetColumn(MainTabs, 2);
            Grid.SetColumnSpan(MainTabs, 1);
            MainTabs.Margin = new Thickness(0);
        }
    }

    private void SetClickThrough(bool enabled)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            var style = GetWindowLong(handle, GwlExStyle);
            var next = enabled ? style | WsExTransparent : style & ~WsExTransparent;
            if (next != style) SetWindowLong(handle, GwlExStyle, next);
        }
        catch
        {
            // Click-through is a best-effort safety net for opacity 0.
        }
    }

    public void ShowNotice(string text, bool error = false)
    {
        FooterNoticeText.Text = text;
        FooterNoticeText.Foreground = error
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x9A))
            : new SolidColorBrush(Color.FromRgb(0x8F, 0xD3, 0xFF));
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_engine is null) return;
        ApplyDisplaySettings(_settings);
        SetClickThrough(Opacity <= 0.001);
        UpdateWrapWidths();
        _engine.MarkPanelViewed();
        RefreshAll();
        if (GameRuntime.GifError is not null)
        {
            ShowNotice("资源完整性异常：" + GameRuntime.GifError, error: true);
        }
        _timer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _timer.Stop();
        _noticeTimer.Stop();
        if (_engine is not null)
        {
            _engine.Changed -= OnEngineChanged;
            _engine.AvatarChanged -= OnAvatarChanged;
            _engine.Toast -= OnEngineToast;
        }
        AvatarGif.Dispose();
    }

    private void OnEngineChanged()
    {
        if (_closed) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_closed) RefreshAll();
        }));
    }

    private void OnAvatarChanged()
    {
        _avatarKey = "";
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_closed) RefreshAll();
        }));
    }

    private void OnEngineToast(string message)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_closed) ShowNotice(message);
        }));
    }

    private void RefreshAll()
    {
        if (_engine is null || _refreshing || _closed) return;
        _refreshing = true;
        try
        {
            RefreshLiveStats();
            RefreshTask();
            RefreshSignIn();
            RefreshDraw();
            RefreshInventory();
            RefreshAchievements();
            UpdateAvatarPreview();
            HeaderHintText.Text = $"累计签到 {_engine.State.TotalSignInDays} 天 · 已解锁动态图 {_engine.UnlockedGifs.Count} 个";
            if (GameRuntime.GifError is not null) HeaderHintText.Text += " · 资源异常";
        }
        catch (Exception error)
        {
            ShowNotice("面板刷新失败：" + error.Message, error: true);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshLiveStats()
    {
        if (_engine is null) return;
        var state = _engine.State;
        LevelBadgeText.Text = "Lv." + state.Level;
        AffectionValueText.Text = _engine.AffectionDisplay;
        AffectionBar.Value = _engine.AffectionProgress * 100;
        MoodValueText.Text = $"{state.Mood}/100";
        MoodBar.Value = state.Mood;
        SatietyValueText.Text = $"{state.Satiety}/100";
        SatietyBar.Value = state.Satiety;
        LevelProgressText.Text = state.Level >= GameEngine.MaxLevel
            ? $"MAX + {state.MaxLevelAffection}"
            : $"{state.Affection}/100 · Lv.{state.Level}";
        LevelBar.Value = _engine.AffectionProgress * 100;
        SignStreakText.Text = $"{state.ContinuousSignIn} 天";
        CompanionText.Text = _engine.CompanionText;
    }

    private void RefreshTask()
    {
        if (_engine is null) return;
        var state = _engine.State;
        var task = _engine.CurrentTask;
        TaskDateText.Text = DateOnly.FromDayNumber(state.TaskDateIndex).ToString("yyyy-MM-dd") + " · 每日 0 点刷新";
        if (task is null)
        {
            TaskNameText.Text = "任务准备中";
            TaskDescriptionText.Text = "今天的任务还没有生成，请稍后重试。";
            TaskStatusText.Text = "未接取";
            TaskProgressBar.Maximum = 1;
            TaskProgressBar.Value = 0;
            TaskProgressText.Text = "进度 0/1";
            TaskRewardText.Text = "";
            TaskAcceptButton.IsEnabled = false;
            TaskAcceptButton.Content = "接取任务";
            return;
        }

        TaskNameText.Text = $"{task.Id} · {task.Name}";
        TaskDescriptionText.Text = task.Description;
        TaskStatusText.Text = _engine.TaskStatusText;
        var current = Math.Clamp(_engine.State.TaskProgress, 0, task.Target);
        TaskProgressBar.Maximum = task.Target;
        TaskProgressBar.Value = current;
        TaskProgressText.Text = $"进度 {current}/{task.Target}";
        TaskRewardText.Text = "奖励：好感 +1、寿司/鱼肉随机 ×1（受心情与签文影响）";
        TaskAcceptButton.Content = _engine.TaskCompleted ? "已完成" : _engine.TaskAccepted ? "已接取" : "接取任务";
        TaskAcceptButton.IsEnabled = !_engine.TaskAccepted && !_engine.TaskCompleted && _engine.CanUseDailyActions;
        TaskRefreshButton.Visibility = Visibility.Collapsed;

        if (_engine.State.TimeRollbackDetected)
        {
            TaskStatusText.Text = "时间异常";
            TaskDescriptionText.Text = _engine.State.TimeRollbackNotice;
        }
    }

    private void RefreshSignIn()
    {
        if (_engine is null) return;
        var state = _engine.State;
        var todayIndex = Math.Clamp(state.LastDateIndex - state.SignWeekStart, 0, 6);
        for (var i = 0; i < _weeklyCells.Count; i++)
        {
            var lit = (state.WeeklySignMask & (1 << i)) != 0;
            var today = i == todayIndex;
            var (border, glyph) = _weeklyCells[i];
            border.Background = new SolidColorBrush(lit
                ? Color.FromRgb(0x2C, 0x6D, 0xA8)
                : today ? Color.FromRgb(0x1B, 0x3D, 0x5E) : Color.FromRgb(0x10, 0x25, 0x3D));
            border.BorderBrush = new SolidColorBrush(today
                ? Color.FromRgb(0x8F, 0xD3, 0xFF)
                : Color.FromRgb(0x2C, 0x5B, 0x84));
            glyph.Text = lit ? "✓" : today ? "今" : "·";
            glyph.Foreground = new SolidColorBrush(lit
                ? Color.FromRgb(0xE8, 0xFB, 0xFF)
                : Color.FromRgb(0x7F, 0xA5, 0xC5));
        }

        SignButton.IsEnabled = !_engine.IsSignedToday && _engine.CanUseDailyActions;
        SignButton.Content = _engine.IsSignedToday ? "今日已签到" : "每日签到";
        SignHintText.Text = $"本周已签到 {_engine.WeeklySignedCount}/7 天 · 连续签到 {state.ContinuousSignIn} 天 · 累计 {state.TotalSignInDays} 天 · 今日奖励寿司";
        if (state.TimeRollbackDetected) SignHintText.Text = state.TimeRollbackNotice;

        Milestone3Text.Text = "集满 3 天 · 寿司 ×1";
        Milestone3StateText.Text = state.Milestone3Claimed ? "本周已领取" : $"{_engine.WeeklySignedCount}/3";
        Milestone7Text.Text = "集满 7 天 · 好感 ×1";
        Milestone7StateText.Text = state.Milestone7Claimed ? "本周已领取" : $"{_engine.WeeklySignedCount}/7";
        Milestone3StateText.Foreground = state.Milestone3Claimed
            ? new SolidColorBrush(Color.FromRgb(0x58, 0xE0, 0xB4))
            : new SolidColorBrush(Color.FromRgb(0x8F, 0xD3, 0xFF));
        Milestone7StateText.Foreground = state.Milestone7Claimed
            ? new SolidColorBrush(Color.FromRgb(0x58, 0xE0, 0xB4))
            : new SolidColorBrush(Color.FromRgb(0x8F, 0xD3, 0xFF));
    }

    private void RefreshDraw()
    {
        if (_engine is null) return;
        var lotMessage = _engine.EnsureDailyLotMessage();
        LotMessageText.Text = string.IsNullOrWhiteSpace(lotMessage) ? "今日签语准备中…" : lotMessage;

        var fortune = _engine.TodayFortune;
        if (fortune == Fortune.None)
        {
            DrawResultText.Text = "未抽签";
            DrawResultText.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xB6, 0xCE));
            DrawHintText.Text = "每日 1 次，0 点刷新。签文只影响抽签后完成的任务。";
            DrawButton.Content = "抽签";
            DrawButton.IsEnabled = _engine.CanUseDailyActions;
        }
        else
        {
            DrawResultText.Text = GameEngine.FortuneName(fortune);
            DrawResultText.Foreground = new SolidColorBrush(fortune switch
            {
                Fortune.Up => Color.FromRgb(0xFF, 0xD7, 0x7A),
                Fortune.Down => Color.FromRgb(0xB8, 0xC4, 0xD6),
                _ => Color.FromRgb(0x8F, 0xE0, 0xC0),
            });
            DrawHintText.Text = fortune switch
            {
                Fortune.Up => "上签：今日任务奖励翻倍（随机食物 ×2、好感 +2）。",
                Fortune.Middle => "中签：今日任务奖励保持原样。",
                _ => "下签：今日任务只给食物，不再增加好感。",
            };
            DrawButton.Content = "今日已抽签";
            DrawButton.IsEnabled = false;
        }
    }

    private void RefreshInventory()
    {
        if (_engine is null) return;
        var capacity = _engine.InventoryCapacity;
        BagCapacityText.Text = $"{_engine.Inventory.Count}/{capacity} 格 · 每升 1 级 +1 格";
        StagingCountText.Text = $"{_engine.Staging.Count}/{GameEngine.MaxStaging}";
        GifEnabledCountText.Text = $"已启用 {_engine.EnabledGifs.Count}/{GameEngine.MaxEnabledGifs}";
        StagingHintText.Text = _engine.Staging.Count >= GameEngine.MaxStaging
            ? "暂存区已满：新物品会直接丢弃。每日 0 点清空。"
            : "最多 20 个物品，每日 0 点清空。背包有空位时会自动移入。";

        var signature = capacity + "|" + string.Join(",", _engine.Inventory)
            + "|" + string.Join(",", _engine.Staging);
        if (signature != _inventorySignature)
        {
            _inventorySignature = signature;
            BuildBagGrid(FoodSlotsPanel, ItemCatalog.FoodCategory);
            BuildBagGrid(IngredientSlotsPanel, ItemCatalog.IngredientCategory);
            BuildStagingGrid();
        }

        var rows = _engine.LoadGifRows().Where(row => row.IsUnlocked).ToList();
        var gifSignature = string.Join("|", rows.Select(row => row.GifId + ":" + row.IsEnabled));
        if (gifSignature != _gifSignature)
        {
            _gifSignature = gifSignature;
            BuildGifWall(rows);
        }
    }

    private void UpdateWrapWidths()
    {
        var width = MainTabs.ActualWidth;
        if (width < 200) return;
        var inner = Math.Max(180, width - 42);
        FoodSlotsPanel.Width = inner;
        IngredientSlotsPanel.Width = inner;
        StagingPanel.Width = inner;
        GifWallPanel.Width = inner;
    }

    private void BuildBagGrid(WrapPanel panel, string category)
    {        if (_engine is null) return;
        panel.Children.Clear();
        var indices = Enumerable.Range(0, _engine.Inventory.Count)
            .Where(index => string.Equals(
                ItemCatalog.Find(_engine.Inventory[index])?.Category, category, StringComparison.Ordinal))
            .ToList();
        var capacity = _engine.InventoryCapacity;
        for (var slot = 0; slot < capacity; slot++)
        {
            var itemId = slot < indices.Count ? _engine.Inventory[indices[slot]] : null;
            var actualIndex = slot < indices.Count ? indices[slot] : -1;
            panel.Children.Add(BuildItemSlot(itemId, actualIndex, clickable: true));
        }
    }

    private void BuildStagingGrid()
    {
        if (_engine is null) return;
        StagingPanel.Children.Clear();
        for (var slot = 0; slot < GameEngine.MaxStaging; slot++)
        {
            var itemId = slot < _engine.Staging.Count ? _engine.Staging[slot] : null;
            StagingPanel.Children.Add(BuildItemSlot(itemId, -1, clickable: false));
        }
    }

    private Border BuildItemSlot(string? itemId, int inventoryIndex, bool clickable)
    {
        var border = new Border
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x22, 0x3A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x5B, 0x84)),
            BorderThickness = new Thickness(1),
            Cursor = clickable && itemId is not null ? Cursors.Hand : Cursors.Arrow,
        };
        if (itemId is not null) WindowDragHelper.SetNoDrag(border, true);
        var def = ItemCatalog.Find(itemId);
        if (def is not null)
        {
            var image = new Image
            {
                Width = 25,
                Height = 25,
                Stretch = Stretch.Uniform,
                Source = ItemCatalog.LoadIcon(def.Id),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            border.Child = image;
            border.ToolTip = $"{def.Name}\n{def.Description}" + (clickable ? "\n点击使用" : "");
            ToolTipService.SetShowDuration(border, 20000);
            AutomationProperties.SetName(border, def.Name);
            border.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x2C, 0x49));
        }
        else
        {
            AutomationProperties.SetName(border, "空格子");
        }

        if (clickable && inventoryIndex >= 0 && itemId is not null)
        {
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                UseItem(inventoryIndex);
            };
        }
        return border;
    }

    private void UseItem(int index)
    {
        if (_engine is null) return;
        var (ok, message) = _engine.UseItem(index);
        ShowNotice(message, !ok);
        _inventorySignature = "";
        RefreshAll();
    }

    private void BuildGifWall(IReadOnlyList<GifUnlockRow> rows)
    {
        GifWallPanel.Children.Clear();
        if (rows.Count == 0)
        {
            GifWallPanel.Children.Add(new TextBlock
            {
                Text = "还没有解锁任何动态图。等级提升或达成成就会解锁，初始不赠送。",
                Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x8C, 0xA6)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4),
            });
            return;
        }

        foreach (var row in rows)
        {
            var source = GifSourceText(row);
            var displayName = GifDisplayName(row.GifId);
            var card = new Border
            {
                Width = 106,
                Margin = new Thickness(0, 0, 5, 8),
                Padding = new Thickness(3),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(row.IsEnabled
                    ? Color.FromRgb(0x17, 0x3A, 0x57)
                    : Color.FromRgb(0x0B, 0x1D, 0x33)),
                BorderBrush = new SolidColorBrush(row.IsEnabled
                    ? Color.FromRgb(0x58, 0xE0, 0xB4)
                    : Color.FromRgb(0x24, 0x45, 0x63)),
                BorderThickness = new Thickness(1),
                ToolTip = $"{row.GifId}\n{displayName}\n{source}\n左键启用/取消，右键预览",
            };
            ToolTipService.SetShowDuration(card, 20000);
            WindowDragHelper.SetNoDrag(card, true);

            var stack = new StackPanel();
            var imageHost = new Grid
            {
                Width = 100,
                Height = 120,
                Background = new SolidColorBrush(Color.FromRgb(0x08, 0x18, 0x30)),
                ClipToBounds = true,
            };
            var thumbnail = LoadGifThumbnail(row.GifId);
            imageHost.Children.Add(new Image
            {
                Width = 100,
                Height = 120,
                Stretch = Stretch.Uniform,
                Source = thumbnail,
            });
            imageHost.MouseEnter += (_, _) => StartWallAnimation(imageHost, row.GifId);
            imageHost.MouseLeave += (_, _) => StopWallAnimation(imageHost);
            imageHost.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ToggleGif(row.GifId);
            };
            imageHost.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                var preview = new GifPreviewWindow(row.GifId, source, displayName) { Owner = this };
                preview.ShowDialog();
            };
            stack.Children.Add(imageHost);

            var footer = new Grid { Width = 100, Margin = new Thickness(0, 4, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameText = new TextBlock
            {
                Text = displayName,
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(row.IsEnabled
                    ? Color.FromRgb(0xE8, 0xFB, 0xFF)
                    : Color.FromRgb(0xC5, 0xD8, 0xE8)),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = displayName,
            };
            var sourceText = new TextBlock
            {
                Text = source,
                FontSize = 7.5,
                Foreground = new SolidColorBrush(row.IsEnabled
                    ? Color.FromRgb(0x8F, 0xE0, 0xC0)
                    : Color.FromRgb(0x7F, 0xA5, 0xC5)),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = source,
            };
            labels.Children.Add(nameText);
            labels.Children.Add(sourceText);
            var check = new CheckBox
            {
                IsChecked = row.IsEnabled,
                Width = 16,
                Height = 16,
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "启用该动态图",
            };
            AutomationProperties.SetName(check, "启用动态图 " + row.GifId);
            check.Click += (_, e) =>
            {
                e.Handled = true;
                ToggleGif(row.GifId);
            };
            Grid.SetColumn(labels, 0);
            Grid.SetColumn(check, 1);
            footer.Children.Add(labels);
            footer.Children.Add(check);
            stack.Children.Add(footer);
            card.Child = stack;
            GifWallPanel.Children.Add(card);
        }
    }

    private void StartWallAnimation(Grid host, string gifId)
    {
        if (host.Tag is GifPlayerControl) return;
        try
        {
            var stream = GameRuntime.Gifs?.GetDecryptedStream(gifId);
            if (stream is null) return;
            var player = new GifPlayerControl
            {
                Width = 100,
                Height = 120,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            player.SetStream(stream);
            host.Children.Add(player);
            host.Tag = player;
        }
        catch
        {
            // A malformed GIF must not break the wall.
        }
    }

    private static void StopWallAnimation(Grid host)
    {
        if (host.Tag is not GifPlayerControl player) return;
        host.Tag = null;
        host.Children.Remove(player);
        player.Dispose();
    }

    private static string GifSourceText(GifUnlockRow row)
    {
        if (string.Equals(row.UnlockType, "Level", StringComparison.OrdinalIgnoreCase))
        {
            return $"{row.UnlockCondition} 级解锁";
        }
        if (string.Equals(row.UnlockType, "Achievement", StringComparison.OrdinalIgnoreCase) &&
            AchievementCatalog.ById.TryGetValue(row.UnlockCondition, out var achievement))
        {
            return achievement.Name;
        }
        return row.UnlockCondition;
    }

    private static string GifDisplayName(string gifId) => GifName.Display(gifId);

    private BitmapSource? LoadGifThumbnail(string gifId)
    {
        if (_gifThumbs.TryGetValue(gifId, out var cached)) return cached;
        try
        {
            using var stream = GameRuntime.Gifs?.GetDecryptedStream(gifId);
            if (stream is null) return null;
            // OnDemand decodes only the first frame; render it down to the
            // 100x120 wall size so 100+ unlocked GIFs stay cheap in memory.
            var decoder = new GifBitmapDecoder(stream,
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            var width = Math.Max(1, frame.PixelWidth);
            var height = Math.Max(1, frame.PixelHeight);
            var fit = Math.Min(100.0 / width, 120.0 / height);
            var drawWidth = width * fit;
            var drawHeight = height * fit;
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawImage(frame, new Rect(
                    (100 - drawWidth) / 2,
                    (120 - drawHeight) / 2,
                    drawWidth,
                    drawHeight));
            }
            var bitmap = new RenderTargetBitmap(100, 120, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            _gifThumbs[gifId] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void ToggleGif(string gifId)
    {
        if (_engine is null) return;
        var enabled = _engine.EnabledGifs.Contains(gifId);
        var (ok, message) = enabled ? _engine.DisableGif(gifId) : _engine.EnableGif(gifId);
        ShowNotice(message, !ok);
        _gifSignature = "";
        RefreshAll();
    }

    private void RefreshAchievements()
    {
        if (_engine is null) return;
        var wall = _engine.AchievementWall().Where(item => item.Visible).ToList();
        foreach (var category in AchievementCatalog.Categories)
        {
            if (!_achievementPanels.TryGetValue(category, out var panel)) continue;
            panel.Children.Clear();
            var items = wall.Where(item => item.Def.Category == category).ToList();
            foreach (var (def, unlocked, at, visible) in items)
            {
                if (!visible) continue;
                var card = new Border
                {
                    Width = 202,
                    Margin = new Thickness(0, 0, 7, 7),
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(unlocked
                        ? Color.FromRgb(0x17, 0x3A, 0x57)
                        : Color.FromRgb(0x0C, 0x1D, 0x33)),
                    BorderBrush = new SolidColorBrush(unlocked
                        ? Color.FromRgb(0x58, 0xE0, 0xB4)
                        : Color.FromRgb(0x24, 0x45, 0x63)),
                    BorderThickness = new Thickness(1),
                    Opacity = unlocked ? 1 : 0.62,
                };
                var stack = new StackPanel();
                var name = new TextBlock
                {
                    Text = unlocked ? "★ " + def.Name : def.Name,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(unlocked
                        ? Color.FromRgb(0xF3, 0xFA, 0xFF)
                        : Color.FromRgb(0x9A, 0xB6, 0xCE)),
                };
                var desc = new TextBlock
                {
                    Text = def.Description,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x8C, 0xA6)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 3),
                };
                var status = new TextBlock
                {
                    Text = unlocked
                        ? "已解锁" + (at is null ? "" : " · " + at.Value.LocalDateTime.ToString("MM-dd HH:mm"))
                        : "未解锁",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(unlocked
                        ? Color.FromRgb(0x58, 0xE0, 0xB4)
                        : Color.FromRgb(0x52, 0x6C, 0x84)),
                };
                stack.Children.Add(name);
                stack.Children.Add(desc);
                stack.Children.Add(status);
                card.Child = stack;
                panel.Children.Add(card);
            }
        }
    }

    private void UpdateAvatarPreview(bool force = false)
    {
        if (_engine is null) return;
        var avatar = _engine.EffectiveAvatar();
        var key = avatar.Kind + "|" + avatar.GifId + "|" + avatar.CustomPath;
        AvatarEmptyBadge.Visibility = Visibility.Collapsed;
        AvatarCaptionText.Text = avatar.Kind switch
        {
            AvatarKind.Gif => avatar.GifId ?? "动态图",
            AvatarKind.CustomImage => "自定义图片",
            AvatarKind.Live2D => "Live2D · 大肥鱼",
            _ => "内置默认形象",
        };

        if (!force && key == _avatarKey) return;
        _avatarKey = key;

        if (avatar.Kind == AvatarKind.Gif)
        {
            try
            {
                var stream = GameRuntime.Gifs?.GetDecryptedStream(avatar.GifId ?? "");
                if (stream is not null)
                {
                    AvatarGif.SetStream(stream);
                    AvatarGif.Visibility = Visibility.Visible;
                    AvatarImage.Visibility = Visibility.Collapsed;
                    return;
                }
            }
            catch (Exception error)
            {
                ShowNotice("动态图加载失败：" + error.Message, error: true);
            }
            AvatarGif.Clear();
            AvatarGif.Visibility = Visibility.Collapsed;
        }
        else
        {
            AvatarGif.Clear();
            AvatarGif.Visibility = Visibility.Collapsed;
        }

        AvatarImage.Visibility = Visibility.Visible;
        AvatarImage.Source = LoadBitmap(avatar.CustomPath) ??
                             LoadBitmap(AssetBootstrapper.PathOf("main.png"));
        if (avatar.Kind == AvatarKind.CustomImage && AvatarImage.Source is null)
        {
            AvatarCaptionText.Text = "自定义图片不可读，已回退默认形象";
        }
    }

    private static BitmapImage? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void BuildWeeklyCells()
    {
        var days = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        for (var i = 0; i < days.Length; i++)
        {
            var border = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(4, 7, 4, 7),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
            };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = days[i],
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x8C, 0xA6)),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var glyph = new TextBlock
            {
                Text = "·",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
            };
            stack.Children.Add(glyph);
            border.Child = stack;
            WeeklySignGrid.Children.Add(border);
            _weeklyCells.Add((border, glyph));
        }
    }

    private void BuildAchievementTabs()
    {
        foreach (var category in AchievementCatalog.Categories)
        {
            var panel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4),
            };
            var scroll = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(2),
            };
            var tab = new TabItem
            {
                Header = category,
                Content = scroll,
                Style = (Style)FindResource("SubTab"),
            };
            AchievementTabs.Items.Add(tab);
            _achievementPanels[category] = panel;
        }
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_engine is null || _refreshing) return;
        switch (MainTabs.SelectedIndex)
        {
            case 1:
                _engine.MarkWeeklyViewed();
                break;
            case 2:
                if (_engine.TodayFortune != Fortune.None) _engine.MarkFortuneViewed();
                break;
            case 3:
                _engine.MarkInventoryViewed();
                break;
            case 4:
                _engine.MarkAchievementsViewed();
                break;
        }
        RefreshAll();
    }

    private void OnBagTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_engine is null || _refreshing) return;
        _inventorySignature = "";
        RefreshInventory();
    }

    private void OnAcceptTaskClick(object sender, RoutedEventArgs e)
    {
        if (_engine is null) return;
        var (ok, message) = _engine.AcceptTask();
        ShowNotice(message, !ok);
        RefreshAll();
    }

    private void OnRefreshTaskClick(object sender, RoutedEventArgs e)
    {
        ShowNotice("任务每天 0 点自动刷新，不能手动更换。");
    }

    private void OnSignClick(object sender, RoutedEventArgs e)
    {
        if (_engine is null) return;
        var (ok, message) = _engine.SignIn();
        ShowNotice(message, !ok);
        RefreshAll();
    }

    private void OnDrawClick(object sender, RoutedEventArgs e)
    {
        if (_engine is null) return;
        var (ok, _, message) = _engine.DrawFortune();
        ShowNotice(message, !ok);
        if (ok) _engine.MarkFortuneViewed();
        RefreshAll();
    }

    private void OnPanelDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (WindowDragHelper.IsInteractive(e.OriginalSource)) return;
        try
        {
            Activate();
            DragMove();
        }
        catch { }
        ClampToWorkArea();
        SavePosition();
    }

    private void OnPanelPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var inner = FindScrollViewer(e.OriginalSource);
        if (inner is not null && !ReferenceEquals(inner, BodyScroll) && CanScroll(inner, e.Delta))
        {
            return;
        }
        if (BodyScroll.ScrollableHeight <= 0) return;
        var lines = Math.Max(1, SystemParameters.WheelScrollLines);
        var delta = Math.Sign(e.Delta) * lines * 16.0;
        BodyScroll.ScrollToVerticalOffset(Math.Clamp(
            BodyScroll.VerticalOffset - delta, 0, BodyScroll.ScrollableHeight));
        e.Handled = true;
    }

    private static bool CanScroll(ScrollViewer viewer, int delta)
    {
        return delta > 0
            ? viewer.VerticalOffset > 0.5
            : viewer.VerticalOffset < viewer.ScrollableHeight - 0.5;
    }

    private static ScrollViewer? FindScrollViewer(object? source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is ScrollViewer viewer) return viewer;
            if (current is Window) return null;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
