using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WhalePet.Game;

namespace WhalePet;

public partial class OrganizerWindow : Window
{
    private const string DragFormat = "WhalePetOrganizerItem";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, out SHFILEINFO info,
        uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    private readonly OrganizerData _data;
    private readonly DispatcherTimer _saveTimer;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private PetSettings _settings;
    private string _currentCategory = "未分类";
    private bool _loaded;
    private bool _internalDrop;
    private Point _dragStart;
    private OrganizerItem? _dragCandidate;

    public event Action<PetSettings>? SettingsChanged;

    public OrganizerWindow(PetSettings settings)
    {
        _settings = settings.Clone();
        _data = DesktopOrganizerStore.Load();
        _currentCategory = _data.DefaultCategory ?? _data.Categories[0];
        if (!_data.Categories.Contains(_currentCategory)) _currentCategory = _data.Categories[0];
        InitializeComponent();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveData();
        };
        ApplySettings(settings);
        BuildCategoryBar();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        PanelPlacementHelper.RestorePosition(this, _data.Left, _data.Top);
        RefreshItems();
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
        Width = Math.Clamp(settings.OrganizerWidth, 300, 1200);
        Height = Math.Clamp(settings.OrganizerHeight, 300, 1000);
        var color = ParseColor(settings.OrganizerBackground, Color.FromRgb(0x08, 0x1A, 0x32));
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(Math.Clamp(settings.OrganizerBackgroundOpacity, 0, 1) * 255),
            color.R, color.G, color.B));
        if (_loaded) PanelPlacementHelper.ClampToVirtual(this);
    }

    private void BuildCategoryBar()
    {
        CategoryBar.Children.Clear();
        for (var categoryIndex = 0; categoryIndex < _data.Categories.Count; categoryIndex++)
        {
            var category = _data.Categories[categoryIndex];
            var isDefault = categoryIndex == 0;
            var button = new Button
            {
                Content = category,
                Height = 28,
                MinWidth = 62,
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 5, 0),
                FontSize = 10.5,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(category == _currentCategory
                    ? Color.FromRgb(0x4D, 0x6B, 0xFE)
                    : Color.FromRgb(0x1F, 0x2C, 0x48)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xDF, 0xF2, 0xFF)),
                BorderBrush = new SolidColorBrush(category == _currentCategory
                    ? Color.FromRgb(0x8F, 0xA3, 0xFF)
                    : Color.FromArgb(0x4D, 0x8F, 0xD3, 0xFF)),
                BorderThickness = new Thickness(1),
            };
            button.Click += (_, _) =>
            {
                _currentCategory = category;
                _selected.Clear();
                BuildCategoryBar();
                RefreshItems();
            };
            button.AllowDrop = true;
            button.DragOver += (_, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DragFormat) || e.Data.GetDataPresent(DataFormats.FileDrop)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
                e.Handled = true;
            };
            button.Drop += (_, e) =>
            {
                if (e.Data.GetData(DragFormat) is string id)
                {
                    var item = _data.Items.FirstOrDefault(entry => entry.Id == id);
                    if (item is not null)
                    {
                        item.Category = category;
                        _internalDrop = true;
                        _currentCategory = category;
                        _selected.Clear();
                        SaveData();
                        BuildCategoryBar();
                        RefreshItems();
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    foreach (var file in files) AddFile(file, category);
                    _currentCategory = category;
                    SaveData();
                    BuildCategoryBar();
                    RefreshItems();
                    e.Handled = true;
                }
            };
            var menu = new ContextMenu { Style = (Style)FindResource("PetContextMenu") };
            var rename = new MenuItem { Header = "重命名分类", Style = (Style)FindResource("PetMenuItem") };
            rename.Click += (_, _) => RenameCategory(category);
            menu.Items.Add(rename);
            if (!isDefault)
            {
                var delete = new MenuItem { Header = "删除分类", Style = (Style)FindResource("PetMenuItem") };
                delete.Click += (_, _) => DeleteCategory(category);
                menu.Items.Add(new Separator { Style = (Style)FindResource("PetSeparator") });
                menu.Items.Add(delete);
            }
            button.ContextMenu = menu;
            CategoryBar.Children.Add(button);
        }
        MoveCategoryBox.ItemsSource = null;
        MoveCategoryBox.ItemsSource = _data.Categories.ToList();
        MoveCategoryBox.SelectedItem = _currentCategory;
    }

    private void RefreshItems()
    {
        if (!IsLoaded) return;
        ItemPanel.Children.Clear();
        var items = _data.Items.Where(item => item.Category == _currentCategory).ToList();
        foreach (var item in items) ItemPanel.Children.Add(BuildItemCard(item));
        CountText.Text = $"{_currentCategory} · {items.Count} 个图标 · 已选 {_selected.Count} 个";
    }

    private Border BuildItemCard(OrganizerItem item)
    {
        var card = new Border
        {
            Width = 112,
            Height = 106,
            Margin = new Thickness(4),
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x3D)),
            BorderBrush = new SolidColorBrush(_selected.Contains(item.Id)
                ? Color.FromRgb(0x58, 0xE0, 0xB4)
                : Color.FromRgb(0x2C, 0x5B, 0x84)),
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand,
            ToolTip = item.TargetPath,
        };
        var stack = new StackPanel();
        var top = new Grid();
        var check = new CheckBox
        {
            IsChecked = _selected.Contains(item.Id),
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "选择",
        };
        check.Click += (_, e) =>
        {
            e.Handled = true;
            if (check.IsChecked == true) _selected.Add(item.Id);
            else _selected.Remove(item.Id);
            RefreshItems();
        };
        top.Children.Add(check);
        stack.Children.Add(top);
        var icon = new Image
        {
            Width = 46,
            Height = 46,
            Stretch = Stretch.Uniform,
            Source = LoadIcon(item.TargetPath),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 3),
        };
        stack.Children.Add(icon);
        stack.Children.Add(new TextBlock
        {
            Text = item.Name,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEA, 0xF6, 0xFF)),
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxHeight = 28,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        card.Child = stack;
        card.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                Launch(item);
                return;
            }
            _dragStart = e.GetPosition(this);
            _dragCandidate = item;
        };
        card.MouseMove += (_, e) =>
        {
            if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(this);
            if (Math.Abs(current.X - _dragStart.X) < 6 && Math.Abs(current.Y - _dragStart.Y) < 6) return;
            var dragging = _dragCandidate;
            _dragCandidate = null;
            StartDrag(card, dragging);
        };
        card.MouseLeftButtonUp += (_, _) => _dragCandidate = null;
        return card;
    }

    private void StartDrag(Border card, OrganizerItem item)
    {
        _internalDrop = false;
        var data = new DataObject();
        data.SetData(DragFormat, item.Id);
        if (File.Exists(item.TargetPath))
        {
            data.SetData(DataFormats.FileDrop, new[] { item.TargetPath });
        }
        var effect = DragDrop.DoDragDrop(card, data, DragDropEffects.Move | DragDropEffects.Copy);
        if (!_internalDrop && effect != DragDropEffects.None)
        {
            _data.Items.Remove(item);
            _selected.Remove(item.Id);
            SaveData();
            RefreshItems();
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) || e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is string id)
        {
            var item = _data.Items.FirstOrDefault(entry => entry.Id == id);
            if (item is not null)
            {
                item.Category = _currentCategory;
                _internalDrop = true;
                SaveData();
                RefreshItems();
            }
            e.Handled = true;
            return;
        }
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var file in files) AddFile(file, _currentCategory);
            SaveData();
            RefreshItems();
            e.Handled = true;
        }
    }

    private void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择桌面软件快捷方式",
            Filter = "快捷方式/程序|*.lnk;*.exe;*.url;*.bat;*.cmd|所有文件|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;
        foreach (var file in dialog.FileNames) AddFile(file, _currentCategory);
        SaveData();
        RefreshItems();
    }

    private void AddFile(string path, string category)
    {
        if (!File.Exists(path)) return;
        if (_data.Items.Any(item => string.Equals(item.TargetPath, path, StringComparison.OrdinalIgnoreCase) &&
                                    item.Category == category))
        {
            return;
        }
        _data.Items.Add(new OrganizerItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Path.GetFileNameWithoutExtension(path),
            TargetPath = path,
            Category = category,
        });
    }

    private string? ValidateCategoryName(string name, string? except = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return "分类名称不能为空。";
        if (name.Length > 20) return "分类名称不能超过 20 个字符。";
        if (_data.Categories.Any(category =>
                !string.Equals(category, except, StringComparison.Ordinal) &&
                string.Equals(category, name, StringComparison.Ordinal)))
        {
            return "分类名称已存在。";
        }
        return null;
    }

    private void OnAddCategoryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TextPromptWindow("新增分类名称", null, 20, name => ValidateCategoryName(name))
        {
            Owner = this,
        };
        if (dialog.ShowDialog() != true) return;
        var name = dialog.Value;
        _data.Categories.Add(name);
        _currentCategory = name;
        SaveData();
        BuildCategoryBar();
        RefreshItems();
    }

    private void RenameCategory(string category)
    {
        var dialog = new TextPromptWindow("重命名分类", category, 20,
            name => ValidateCategoryName(name, category))
        {
            Owner = this,
        };
        if (dialog.ShowDialog() != true) return;
        var name = dialog.Value;
        if (name == category) return;
        var index = _data.Categories.IndexOf(category);
        if (index < 0) return;
        _data.Categories[index] = name;
        foreach (var item in _data.Items.Where(item => item.Category == category))
            item.Category = name;
        if (_currentCategory == category) _currentCategory = name;
        if (string.Equals(_data.DefaultCategory, category, StringComparison.Ordinal))
            _data.DefaultCategory = name;
        SaveData();
        BuildCategoryBar();
        RefreshItems();
    }

    private void DeleteCategory(string category)
    {
        if (string.Equals(category, _data.DefaultCategory, StringComparison.Ordinal)) return;
        var fallback = _data.DefaultCategory ?? _data.Categories[0];
        var count = _data.Items.Count(item => item.Category == category);
        var message = count == 0
            ? $"删除分类“{category}”？"
            : $"分类“{category}”内有 {count} 个图标，删除后这些图标会移动到“{fallback}”。继续？";
        if (!ConfirmDialog.Show(this, message, "删除分类", "删除", _settings.ThemeColor)) return;
        foreach (var item in _data.Items.Where(item => item.Category == category))
            item.Category = fallback;
        _data.Categories.Remove(category);
        if (_currentCategory == category) _currentCategory = fallback;
        SaveData();
        BuildCategoryBar();
        RefreshItems();
    }

    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0)
        {
            ConfirmDialog.ShowInfo(this, "请先勾选要删除的图标。", "批量删除", _settings.ThemeColor);
            return;
        }
        if (!ConfirmDialog.Show(this, $"删除选中的 {_selected.Count} 个图标？", "批量删除",
                "删除", _settings.ThemeColor)) return;
        _data.Items.RemoveAll(item => _selected.Contains(item.Id));
        _selected.Clear();
        SaveData();
        RefreshItems();
    }

    private void OnMoveSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_selected.Count == 0)
        {
            ConfirmDialog.ShowInfo(this, "请先勾选要移动的图标。", "批量移动", _settings.ThemeColor);
            return;
        }
        var target = MoveCategoryBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(target)) return;
        foreach (var item in _data.Items.Where(item => _selected.Contains(item.Id)))
            item.Category = target;
        _selected.Clear();
        SaveData();
        _currentCategory = target;
        BuildCategoryBar();
        RefreshItems();
    }

    private void Launch(OrganizerItem item)
    {
        try
        {
            if (File.Exists(item.TargetPath))
            {
                Process.Start(new ProcessStartInfo(item.TargetPath) { UseShellExecute = true });
            }
        }
        catch (Exception error)
        {
            ConfirmDialog.ShowInfo(this, "启动失败：" + error.Message, "桌面图标收纳区",
                _settings.ThemeColor);
        }
    }

    private void SaveData()
    {
        _data.Left = Left;
        _data.Top = Top;
        _data.Width = Width;
        _data.Height = Height;
        _data.DefaultCategory ??= _data.Categories.FirstOrDefault() ?? "未分类";
        _settings.OrganizerWidth = Width;
        _settings.OrganizerHeight = Height;
        SettingsChanged?.Invoke(_settings.Clone());
        DesktopOrganizerStore.Save(_data);
    }

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

    private static ImageSource? LoadIcon(string path)
    {
        try
        {
            var info = new SHFILEINFO();
            var result = SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100);
            if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }
        }
        catch
        {
            // Fall back to no icon.
        }
        return null;
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
