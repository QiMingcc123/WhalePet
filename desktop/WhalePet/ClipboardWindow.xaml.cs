using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace WhalePet;

public partial class ClipboardWindow : Window
{
    private readonly ObservableCollection<ClipboardEntry> _entries;
    private bool _suppressReload;

    public ClipboardWindow()
    {
        InitializeComponent();
        var settings = SettingsStore.Load();
        Background = ThemePalette.Surface(
            settings.ThemeColor,
            settings.BackgroundImageEnabled,
            settings.BackgroundImagePath,
            settings.BackgroundImageOpacity,
            0.16);
        var accent = ThemePalette.Parse(settings.ThemeColor, Color.FromRgb(0x4D, 0x6B, 0xFE));
        Resources["AccentFill"] = ThemePalette.Brush(accent, 0.18);
        Resources["AccentBorder"] = ThemePalette.Brush(ThemePalette.Lighten(accent, 0.25), 0.85);
        _entries = new ObservableCollection<ClipboardEntry>(ClipboardStore.Load());
        EntryList.ItemsSource = _entries;
        ClipboardStore.Changed += OnClipboardStoreChanged;
        Closed += (_, _) => ClipboardStore.Changed -= OnClipboardStoreChanged;
        UpdateStatus();
    }

    private void OnClipboardStoreChanged(object? sender, EventArgs e)
    {
        if (_suppressReload) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsLoaded || _suppressReload) return;
            ReloadEntries();
        }));
    }

    private void ReloadEntries()
    {
        var latest = ClipboardStore.Load();
        if (latest.Count == _entries.Count
            && latest.Select(entry => entry.Id).SequenceEqual(_entries.Select(entry => entry.Id)))
        {
            return;
        }
        _entries.Clear();
        foreach (var entry in latest) _entries.Add(entry);
        UpdateStatus();
    }

    private void SaveLocal()
    {
        _suppressReload = true;
        try
        {
            ClipboardStore.Save(_entries);
            ClipboardStore.NotifyChanged();
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private void UpdateStatus()
    {
        EmptyHint.Visibility = _entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = $"共 {_entries.Count} 条 · 最多保留 {ClipboardStore.MaxEntries} 条";
    }

    private void OnCopyEntry(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ClipboardEntry entry }) return;
        try
        {
            Clipboard.SetText(entry.Text);
            StatusText.Text = "已复制到剪贴板。";
        }
        catch
        {
            StatusText.Text = "剪贴板被其他程序占用，请稍后再试。";
        }
    }

    private void OnDeleteEntry(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ClipboardEntry entry }) return;
        _entries.Remove(entry);
        SaveLocal();
        UpdateStatus();
    }

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0) return;
        if (!ConfirmDialog.Show(this, $"确定清空全部 {_entries.Count} 条剪贴板历史吗？", "确认清空", "清空"))
        {
            return;
        }
        _entries.Clear();
        SaveLocal();
        UpdateStatus();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
