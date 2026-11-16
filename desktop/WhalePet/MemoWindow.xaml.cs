using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace WhalePet;

public partial class MemoWindow : Window
{
    public static readonly DependencyProperty BulkModeProperty = DependencyProperty.Register(
        nameof(BulkMode), typeof(bool), typeof(MemoWindow), new PropertyMetadata(false));

    private readonly ObservableCollection<MemoItem> _memos;
    private readonly ListCollectionView _view;
    private readonly string _themeColor;

    public bool BulkMode
    {
        get => (bool)GetValue(BulkModeProperty);
        set => SetValue(BulkModeProperty, value);
    }

    public MemoWindow(PetSettings settings)
    {
        InitializeComponent();
        Resources["MemoTextBrush"] = Brush(settings.MemoTextColor);
        Resources["MemoFontSize"] = settings.MemoFontSize;
        var lineHeight = settings.MemoFontSize + 12;
        Resources["MemoLineHeight"] = lineHeight;
        Resources["PaperLines"] = CreatePaperBrush(lineHeight);
        var theme = Brush(settings.ThemeColor);
        _themeColor = settings.ThemeColor;
        AddButton.Background = theme;
        AddButton.BorderBrush = theme;
        var scale = Math.Clamp(settings.MemoUiScale, 0.5, 2);
        Width = Math.Min(1100, 560 * scale);
        Height = Math.Min(1000, 640 * scale);
        MinHeight = Math.Min(760, 500 * scale);
        _memos = new ObservableCollection<MemoItem>(MemoStore.Load());
        _view = new ListCollectionView(_memos)
        {
            Filter = item => MatchesSearch((MemoItem)item) && MatchesTime((MemoItem)item),
        };
        MemoList.ItemsSource = _view;
        MemoInputPlaceholder.Visibility = Visibility.Visible;
        MemoSearchPlaceholder.Visibility = Visibility.Visible;
        UpdateHint();
    }

    private static DrawingBrush CreatePaperBrush(double lineHeight)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0xFB, 0xF5, 0xE5)),
            null,
            new RectangleGeometry(new Rect(0, 0, 1000, lineHeight))));
        group.Children.Add(new GeometryDrawing(
            null,
            new Pen(new SolidColorBrush(Color.FromRgb(0xD8, 0xC4, 0x9A)), 0.8),
            new LineGeometry(new Point(0, lineHeight - 0.5), new Point(1000, lineHeight - 0.5))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 1000, lineHeight),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        return brush;
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

    private void OnAddMemo(object sender, RoutedEventArgs e)
    {
        var text = MemoInput.Text.Trim();
        if (text.Length == 0)
        {
            ErrorText.Text = "先写点什么吧。";
            return;
        }

        var memo = new MemoItem { Text = text.Length > 2000 ? text[..2000] : text, CreatedAt = DateTime.Now };
        _memos.Insert(0, memo);
        while (_memos.Count > 100) _memos.RemoveAt(_memos.Count - 1);
        MemoStore.Save(_memos);
        MemoInput.Clear();
        ErrorText.Text = "";
        UpdateHint();
    }

    private void OnMemoContentClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MemoItem memo }) return;
        memo.Done = !memo.Done;
        MemoStore.Save(_memos);
        UpdateHint();
        e.Handled = true;
    }

    private void OnEditMemo(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MemoItem memo }) return;
        var dialog = new MemoEditWindow(memo.Text) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        memo.Text = dialog.EditedText;
        MemoStore.Save(_memos);
        _view.Refresh();
        UpdateHint();
    }

    private void OnDeleteMemo(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MemoItem memo }) return;
        if (!ConfirmDelete(1, memo.Text)) return;
        _memos.Remove(memo);
        MemoStore.Save(_memos);
        UpdateHint();
    }

    private void OnToggleBulk(object sender, RoutedEventArgs e)
    {
        BulkMode = !BulkMode;
        BulkToggleButton.Content = BulkMode ? "退出批量" : "批量删除";
        var visibility = BulkMode ? Visibility.Visible : Visibility.Collapsed;
        BulkSelectAllButton.Visibility = visibility;
        BulkDeleteButton.Visibility = visibility;
        BulkSelectAllButton.Content = "全选";
        BulkTip.Text = BulkMode ? "勾选要删除的备忘录，再点“删除选中”。" : "";
        if (!BulkMode)
        {
            ClearSelection();
        }
        UpdateHint();
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        var visible = _view.Cast<MemoItem>().ToList();
        var allSelected = visible.Count > 0 && visible.All(memo => memo.Selected);
        foreach (var memo in visible) memo.Selected = !allSelected;
        BulkSelectAllButton.Content = allSelected ? "全选" : "取消全选";
        UpdateHint();
    }

    private void OnBulkDelete(object sender, RoutedEventArgs e)
    {
        var selected = _view.Cast<MemoItem>().Where(memo => memo.Selected).ToList();
        if (selected.Count == 0)
        {
            ErrorText.Text = "请先勾选要删除的备忘录。";
            return;
        }
        if (!ConfirmDelete(selected.Count)) return;

        foreach (var memo in selected) _memos.Remove(memo);
        MemoStore.Save(_memos);
        ErrorText.Text = "";
        BulkSelectAllButton.Content = "全选";
        UpdateHint();
    }

    private void ClearSelection()
    {
        if (_memos is null) return;
        foreach (var memo in _memos) memo.Selected = false;
        if (BulkMode) BulkSelectAllButton.Content = "全选";
    }

    private bool ConfirmDelete(int count, string? preview = null)
    {
        var message = count == 1 && !string.IsNullOrWhiteSpace(preview)
            ? $"确定删除这条备忘录吗？\n\n{(preview.Length > 80 ? preview[..80] + "…" : preview)}"
            : $"确定删除选中的 {count} 条备忘录吗？";
        return ConfirmDialog.Show(this, message + "\n\n此操作不可恢复。", "确认删除", "删除", _themeColor);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (MemoSearchPlaceholder is not null)
        {
            MemoSearchPlaceholder.Visibility = string.IsNullOrEmpty(MemoSearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        _view?.Refresh();
        ClearSelection();
        UpdateHint();
    }

    private void OnMemoInputChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (MemoInputPlaceholder is not null)
        {
            MemoInputPlaceholder.Visibility = string.IsNullOrWhiteSpace(MemoInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private bool MatchesSearch(MemoItem memo)
    {
        var query = MemoSearchBox?.Text.Trim() ?? "";
        if (query.Length == 0) return true;
        return memo.Text.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesTime(MemoItem memo)
    {
        var tag = (MemoTimeFilter?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "all";
        if (tag == "all") return true;

        var now = DateTime.Now;
        var createdAt = memo.CreatedAt;
        return tag switch
        {
            "today" => createdAt.Date == now.Date,
            "3d" => createdAt >= now.AddDays(-3),
            "7d" => createdAt >= now.AddDays(-7),
            "30d" => createdAt >= now.AddDays(-30),
            "month" => createdAt.Year == now.Year && createdAt.Month == now.Month,
            _ => true,
        };
    }

    private string TimeFilterLabel =>
        (MemoTimeFilter?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content as string ?? "全部时间";

    private void OnTimeFilterChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MemoTimeFilter is not null) MemoTimeFilter.IsDropDownOpen = false;
        _view?.Refresh();
        ClearSelection();
        UpdateHint();
    }

    private void UpdateHint()
    {
        var total = _memos?.Count ?? 0;
        var filtered = _view?.Count ?? total;
        var selected = _memos?.Count(memo => memo.Selected) ?? 0;
        MemoHint.Text = $"保存在本机，共 {total} 条" +
            (filtered != total ? $" · 当前显示 {filtered} 条" : "") +
            (BulkMode ? $" · 已选 {selected} 条" : "") +
            $" · 时间筛选：{TimeFilterLabel}" +
            $" · {MemoStore.FilePath}";
    }
}
