using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WhalePet.Game;

namespace WhalePet;

public partial class TodoWindow : Window
{
    private readonly TodoData _data;
    private readonly DispatcherTimer _saveTimer;
    private readonly HashSet<string> _timeFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _urgencyFilters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleButton> _timeButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToggleButton> _urgencyButtons = new(StringComparer.Ordinal);
    private PetSettings _settings;
    private bool _loaded;
    private string _query = "";
    private DateTime? _customStart;
    private DateTime? _customEnd;
    private FrameworkElement? _firstHit;

    public event Action<PetSettings>? SettingsChanged;

    public TodoWindow(PetSettings settings)
    {
        _settings = settings.Clone();
        _data = TodoStore.Load();
        _timeFilters.UnionWith(_data.TimeFilters);
        _urgencyFilters.UnionWith(_data.UrgencyFilters);
        _customStart = _data.CustomStart;
        _customEnd = _data.CustomEnd;
        if (_customStart is null || _customEnd is null) _timeFilters.Remove("自定义");
        InitializeComponent();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveBounds();
        };
        ApplySettings(settings);
        BuildFilterButtons();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        PanelPlacementHelper.RestorePosition(this, _data.Left, _data.Top);
        RefreshList();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        PanelPlacementHelper.AttachDisplayWatcher(this, SaveData);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
    }

    public void ApplySettings(PetSettings settings)
    {
        _settings = settings.Clone();
        Width = Math.Clamp(settings.TodoWidth, 300, 1200);
        Height = Math.Clamp(settings.TodoHeight, 300, 1000);
        var color = ParseColor(settings.TodoBackground, Color.FromRgb(0x08, 0x1A, 0x32));
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(Math.Clamp(settings.TodoBackgroundOpacity, 0, 1) * 255),
            color.R, color.G, color.B));
        if (_loaded) PanelPlacementHelper.ClampToVirtual(this);
    }

    private void BuildFilterButtons()
    {
        TimeFilterPanel.Children.Clear();
        _timeButtons.Clear();
        AddTimeButton("全部", "全部");
        foreach (var label in new[] { "今天", "昨天", "本周", "上周", "本月", "上月" })
            AddTimeButton(label, label);
        AddTimeButton("自定义", "自定义");

        UrgencyFilterPanel.Children.Clear();
        _urgencyButtons.Clear();
        AddUrgencyButton("全部", null);
        foreach (var (key, name, color) in TodoPalette.Urgencies)
            AddUrgencyButton(name, color, key);

        UpdateFilterStates();
    }

    private void AddTimeButton(string label, string key)
    {
        var button = new ToggleButton
        {
            Content = label,
            Tag = key,
            Style = (Style)FindResource("TodoFilterChip"),
        };
        button.Click += OnTimeFilterClick;
        TimeFilterPanel.Children.Add(button);
        _timeButtons[key] = button;
    }

    private void AddUrgencyButton(string label, Color? color, string? key = null)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (color is { } dotColor)
        {
            content.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(dotColor),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        content.Children.Add(new TextBlock
        {
            Text = label,
            Margin = color is null ? new Thickness(0) : new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEA, 0xF6, 0xFF)),
        });
        var button = new ToggleButton
        {
            Content = content,
            Tag = key ?? "全部",
            Style = (Style)FindResource("TodoFilterChip"),
        };
        button.Click += OnUrgencyFilterClick;
        UrgencyFilterPanel.Children.Add(button);
        _urgencyButtons[key ?? "全部"] = button;
    }

    private void OnTimeFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string key }) return;
        if (key == "全部")
        {
            _timeFilters.Clear();
            _customStart = null;
            _customEnd = null;
        }
        else if (key == "自定义")
        {
            OpenCustomRange();
        }
        else if (!_timeFilters.Remove(key))
        {
            _timeFilters.Add(key);
        }
        UpdateFilterStates();
        RefreshList();
        SaveData();
    }

    private void OnUrgencyFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string key }) return;
        if (key == "全部")
        {
            _urgencyFilters.Clear();
        }
        else if (!_urgencyFilters.Remove(key))
        {
            _urgencyFilters.Add(key);
        }
        UpdateFilterStates();
        RefreshList();
        SaveData();
    }

    private void OpenCustomRange()
    {
        var dialog = new TodoDateRangeWindow(_customStart, _customEnd) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (dialog.Cleared)
        {
            _customStart = null;
            _customEnd = null;
            _timeFilters.Remove("自定义");
        }
        else
        {
            _customStart = dialog.StartDate;
            _customEnd = dialog.EndDate;
            _timeFilters.Add("自定义");
        }
        UpdateFilterStates();
        RefreshList();
        SaveData();
    }

    private void UpdateFilterStates()
    {
        foreach (var (key, button) in _timeButtons)
        {
            button.IsChecked = key == "全部"
                ? _timeFilters.Count == 0
                : _timeFilters.Contains(key);
        }
        if (_timeButtons.TryGetValue("自定义", out var customButton))
        {
            customButton.Content = _customStart is { } start && _customEnd is { } end
                ? $"{start:MM.dd}-{end:MM.dd}"
                : "自定义";
        }
        foreach (var (key, button) in _urgencyButtons)
        {
            button.IsChecked = key == "全部"
                ? _urgencyFilters.Count == 0
                : _urgencyFilters.Contains(key);
        }
    }

    private void RefreshList()
    {
        if (!IsLoaded) return;
        ItemsPanel.Children.Clear();
        _firstHit = null;
        var items = _data.Items
            .Where(MatchesTime)
            .Where(MatchesUrgency)
            .Where(MatchesQuery)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
        foreach (var item in items)
        {
            var card = BuildCard(item);
            ItemsPanel.Children.Add(card);
            if (_firstHit is null && _query.Length > 0) _firstHit = card;
        }
        CountText.Text = $"{items.Count} 项";
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilterHintText.Text = _timeFilters.Count > 0 || _urgencyFilters.Count > 0
            ? "多选筛选取并集，搜索与筛选取交集"
            : "";
    }

    private bool MatchesTime(TodoItem item)
    {
        if (_timeFilters.Count == 0) return true;
        var date = item.CreatedAt.LocalDateTime.Date;
        var today = DateTime.Today;
        foreach (var filter in _timeFilters)
        {
            switch (filter)
            {
                case "今天" when date == today:
                case "昨天" when date == today.AddDays(-1):
                    return true;
                case "本周" when InRange(date, StartOfWeek(today), today):
                case "上周" when InRange(date, StartOfWeek(today).AddDays(-7),
                    StartOfWeek(today).AddDays(-1)):
                case "本月" when InRange(date, new DateTime(today.Year, today.Month, 1), today):
                case "上月" when InRange(date,
                    new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                    new DateTime(today.Year, today.Month, 1).AddDays(-1)):
                    return true;
                case "自定义" when _customStart is { } start && _customEnd is { } end &&
                                  InRange(date, start.Date, end.Date):
                    return true;
            }
        }
        return false;
    }

    private bool MatchesUrgency(TodoItem item) =>
        _urgencyFilters.Count == 0 || _urgencyFilters.Contains(item.Urgency);

    private bool MatchesQuery(TodoItem item)
    {
        if (_query.Length == 0) return true;
        return item.Title.Contains(_query, StringComparison.CurrentCultureIgnoreCase) ||
               (item.Notes ?? "").Contains(_query, StringComparison.CurrentCultureIgnoreCase);
    }

    private static DateTime StartOfWeek(DateTime day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static bool InRange(DateTime date, DateTime start, DateTime end) =>
        date >= start.Date && date <= end.Date;

    private Border BuildCard(TodoItem item)
    {
        var color = TodoPalette.ColorOf(item.Urgency);
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x3D)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(9, 7, 9, 7),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = Cursors.Hand,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 8, 0),
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(BuildHighlightedText(item.Title, 11, FontWeights.SemiBold,
            new SolidColorBrush(Color.FromRgb(0xEA, 0xF6, 0xFF))));
        if (!string.IsNullOrWhiteSpace(item.Notes))
        {
            var notes = BuildHighlightedText(item.Notes ?? "", 9.5, FontWeights.Normal,
                new SolidColorBrush(Color.FromRgb(0x9A, 0xB6, 0xCE)));
            notes.Margin = new Thickness(0, 3, 0, 0);
            notes.MaxHeight = 38;
            stack.Children.Add(notes);
        }
        stack.Children.Add(new TextBlock
        {
            Text = $"{TodoPalette.NameOf(item.Urgency)} · {TodoPalette.StatusName(item.Status)} · " +
                   item.CreatedAt.LocalDateTime.ToString("MM-dd HH:mm"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA5, 0xC5)),
            FontSize = 9,
            Margin = new Thickness(0, 4, 0, 0),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var statusButton = new Button
        {
            Content = item.Status == "Completed" ? "恢复" : "完成",
            FontSize = 9,
            Height = 22,
            Margin = new Thickness(4, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x58, 0xE0, 0xB4)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCF, 0xEA, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x8F, 0xD3, 0xFF)),
        };
        statusButton.Click += (_, e) =>
        {
            e.Handled = true;
            item.Status = item.Status == "Completed" ? "NotStarted" : "Completed";
            item.UpdatedAt = DateTimeOffset.Now;
            SaveData();
            RefreshList();
        };
        var edit = new Button
        {
            Content = "编辑",
            FontSize = 9,
            Height = 22,
            Margin = new Thickness(4, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x1F, 0x8F, 0xD3, 0xFF)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCF, 0xEA, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0x8F, 0xD3, 0xFF)),
        };
        edit.Click += (_, e) =>
        {
            e.Handled = true;
            EditItem(item);
        };
        var delete = new Button
        {
            Content = "删除",
            FontSize = 9,
            Height = 22,
            Margin = new Thickness(4, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x6B, 0x6B)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xE1, 0xE1)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0x8A, 0x8A)),
        };
        delete.Click += (_, e) =>
        {
            e.Handled = true;
            DeleteItem(item);
        };
        buttons.Children.Add(statusButton);
        buttons.Children.Add(edit);
        buttons.Children.Add(delete);

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(stack, 1);
        Grid.SetColumn(buttons, 2);
        grid.Children.Add(dot);
        grid.Children.Add(stack);
        grid.Children.Add(buttons);
        card.Child = grid;
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                EditItem(item);
            }
        };
        card.ContextMenu = BuildCardContextMenu(item);
        return card;
    }

    private ContextMenu BuildCardContextMenu(TodoItem item)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("PetContextMenu") };
        var edit = new MenuItem { Header = "编辑", Style = (Style)FindResource("PetMenuItem") };
        edit.Click += (_, _) => EditItem(item);
        var status = new MenuItem
        {
            Header = item.Status == "Completed" ? "恢复待办" : "标记完成",
            Style = (Style)FindResource("PetMenuItem"),
        };
        status.Click += (_, _) =>
        {
            item.Status = item.Status == "Completed" ? "NotStarted" : "Completed";
            item.UpdatedAt = DateTimeOffset.Now;
            SaveData();
            RefreshList();
        };
        var delete = new MenuItem { Header = "删除", Style = (Style)FindResource("PetMenuItem") };
        delete.Click += (_, _) => DeleteItem(item);
        menu.Items.Add(edit);
        menu.Items.Add(status);
        menu.Items.Add(new Separator { Style = (Style)FindResource("PetSeparator") });
        menu.Items.Add(delete);
        return menu;
    }

    private TextBlock BuildHighlightedText(string text, double fontSize, FontWeight weight, Brush foreground)
    {
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground,
        };
        if (_query.Length == 0)
        {
            block.Text = text;
            return block;
        }
        var index = 0;
        while (index < text.Length)
        {
            var hit = text.IndexOf(_query, index, StringComparison.CurrentCultureIgnoreCase);
            if (hit < 0)
            {
                block.Inlines.Add(new Run(text[index..]));
                break;
            }
            if (hit > index) block.Inlines.Add(new Run(text[index..hit]));
            block.Inlines.Add(new Run(text.Substring(hit, _query.Length))
            {
                Background = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xC1, 0x4D)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x08, 0x1A, 0x32)),
                FontWeight = FontWeights.SemiBold,
            });
            index = hit + _query.Length;
        }
        return block;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TodoEditWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _data.Items.Add(dialog.Result);
        SaveData();
        RefreshList();
    }

    private void EditItem(TodoItem item)
    {
        var dialog = new TodoEditWindow(item) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        item.Title = dialog.Result.Title;
        item.Notes = dialog.Result.Notes;
        item.Urgency = dialog.Result.Urgency;
        item.Status = dialog.Result.Status;
        item.UpdatedAt = DateTimeOffset.Now;
        SaveData();
        RefreshList();
    }

    private void DeleteItem(TodoItem item)
    {
        if (!ConfirmDialog.Show(this, $"删除待办“{item.Title}”？", "桌面待办",
                "删除", _settings.ThemeColor)) return;
        _data.Items.Remove(item);
        SaveData();
        RefreshList();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        ClearSearchButton.Visibility = SearchBox.Text.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        RefreshList();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _firstHit?.BringIntoView();
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void OnClearFilterClick(object sender, RoutedEventArgs e)
    {
        _timeFilters.Clear();
        _urgencyFilters.Clear();
        _customStart = null;
        _customEnd = null;
        UpdateFilterStates();
        RefreshList();
        SaveData();
    }

    private void SaveData()
    {
        _data.Left = Left;
        _data.Top = Top;
        _data.Width = Width;
        _data.Height = Height;
        _data.TimeFilters = _timeFilters.ToList();
        _data.UrgencyFilters = _urgencyFilters.ToList();
        _data.CustomStart = _customStart;
        _data.CustomEnd = _customEnd;
        _settings.TodoWidth = Width;
        _settings.TodoHeight = Height;
        SettingsChanged?.Invoke(_settings.Clone());
        TodoStore.Save(_data);
    }

    private void SaveBounds() => SaveData();

    private void OnBoundsChanged(object? sender, EventArgs e)
    {
        if (!_loaded) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (WindowDragHelper.IsInteractive(e.OriginalSource)) return;
        var width = Width;
        var height = Height;
        try { DragMove(); } catch { }
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        Width = width;
        Height = height;
        PanelPlacementHelper.SnapToEdge(this);
        SaveData();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _saveTimer.Stop();
        SaveData();
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
