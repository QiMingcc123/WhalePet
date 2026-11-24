using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhalePet.Game;

public partial class AvatarSwitchWindow : Window
{
    private sealed record Option(string Kind, string Id, string Name, DefaultAvatar? Avatar, string? GifId);

    private readonly GameEngine _engine;
    private readonly List<(Border Border, Option Option)> _cards = new();
    private readonly List<GifPlayerControl> _players = new();
    private string? _selectedDefaultId;
    private string? _selectedGifId;

    public AvatarSwitchWindow(GameEngine engine)
    {
        _engine = engine;
        InitializeComponent();
        BuildDefaultCards();
        BuildDynamicCards();
        SelectCurrent();
        UpdateSelection();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Keyboard.Focus(this);
    }

    private void BuildDefaultCards()
    {
        DefaultPanel.Children.Clear();
        var options = _engine.DefaultAvatars
            .Where(avatar => avatar.IsBuiltIn || avatar.Enabled)
            .ToList();
        var customIndex = 0;
        foreach (var avatar in options)
        {
            if (!avatar.IsBuiltIn) customIndex++;
            var name = avatar.IsBuiltIn ? "内置默认形象" : $"自定义主形象 {customIndex}";
            var image = LoadDefaultImage(avatar);
            var card = BuildCard(name, avatar.IsBuiltIn ? "内置 · 不可删除" : "自定义 · 默认启用",
                image, null, out var option);
            option = new Option("default", avatar.Id, name, avatar, null);
            card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _selectedDefaultId = avatar.Id;
                _selectedGifId = null;
                UpdateSelection();
            };
            _cards.Add((card, option));
            DefaultPanel.Children.Add(card);
        }
        if (options.Count == 0)
        {
            DefaultPanel.Children.Add(new TextBlock
            {
                Text = "没有可用的默认主形象。",
                Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA5, 0xC5)),
                FontSize = 11,
                Margin = new Thickness(6),
            });
        }
    }

    private void BuildDynamicCards()
    {
        DynamicPanel.Children.Clear();
        foreach (var id in _engine.EnabledGifs.OrderBy(id => id, StringComparer.Ordinal))
        {
            var name = GifName.Display(id);
            var card = BuildCard(name, "已启用动态图", null, id, out var option);
            option = new Option("gif", id, name, null, id);
            card.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _selectedGifId = id;
                _selectedDefaultId = null;
                UpdateSelection();
            };
            _cards.Add((card, option));
            DynamicPanel.Children.Add(card);
        }
        if (_engine.EnabledGifs.Count == 0)
        {
            DynamicPanel.Children.Add(new TextBlock
            {
                Text = "尚未启用任何动态图，请到角色面板 → 背包 → 动态图墙启用。",
                Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA5, 0xC5)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6),
            });
        }
    }

    private Border BuildCard(string name, string subtitle, BitmapSource? image, string? gifId,
        out Option option)
    {
        var card = new Border
        {
            Width = 190,
            Height = 166,
            Margin = new Thickness(5),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x1D, 0x33)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x5B, 0x84)),
            BorderThickness = new Thickness(2),
            Cursor = Cursors.Hand,
        };
        var stack = new StackPanel();
        var imageHost = new Grid
        {
            Width = 170,
            Height = 100,
            Background = new SolidColorBrush(Color.FromRgb(0x08, 0x18, 0x30)),
            ClipToBounds = true,
        };
        if (image is not null)
        {
            imageHost.Children.Add(new Image { Source = image, Stretch = Stretch.Uniform });
        }
        if (gifId is not null)
        {
            try
            {
                var stream = GameRuntime.Gifs?.GetDecryptedStream(gifId);
                if (stream is not null)
                {
                    var player = new GifPlayerControl { Width = 170, Height = 100 };
                    player.SetStream(stream);
                    imageHost.Children.Add(player);
                    _players.Add(player);
                }
            }
            catch
            {
                // Keep the label if the GIF cannot be decoded.
            }
        }
        stack.Children.Add(imageHost);
        stack.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xFA, 0xFF)),
            Margin = new Thickness(2, 5, 2, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0xA5, 0xC5)),
            Margin = new Thickness(2, 2, 2, 0),
        });
        card.Child = stack;
        card.ToolTip = gifId is null ? name : $"{gifId}\n显示名：{name}";
        option = new Option("", "", name, null, gifId);
        return card;
    }

    private void SelectCurrent()
    {
        var avatar = _engine.EffectiveAvatar();
        if (avatar.Kind == AvatarKind.Gif)
        {
            _selectedGifId = _engine.EnabledGifs.Contains(avatar.GifId ?? "")
                ? avatar.GifId
                : null;
            SwitchTabs.SelectedIndex = 1;
            return;
        }

        SwitchTabs.SelectedIndex = 0;
        if (avatar.Kind == AvatarKind.DefaultStatic)
        {
            _selectedDefaultId = _engine.BuiltInDefaultAvatar?.Id;
            return;
        }

        var currentData = _engine.State.CurrentCustomImageData;
        if (string.IsNullOrWhiteSpace(currentData) &&
            !string.IsNullOrWhiteSpace(_engine.State.CurrentCustomImagePath) &&
            File.Exists(_engine.State.CurrentCustomImagePath))
        {
            try
            {
                currentData = Convert.ToBase64String(File.ReadAllBytes(_engine.State.CurrentCustomImagePath));
            }
            catch
            {
                currentData = null;
            }
        }
        _selectedDefaultId = _engine.DefaultAvatars
            .Where(item => !item.IsBuiltIn && item.Enabled)
            .FirstOrDefault(item => string.Equals(item.Data, currentData, StringComparison.Ordinal))
            ?.Id;
    }

    private void UpdateSelection()
    {
        if (ApplyButton is null || CaptionText is null || DefaultHintText is null ||
            DynamicHintText is null)
        {
            return;
        }
        foreach (var (border, option) in _cards)
        {
            var selected = option.Kind == "default"
                ? string.Equals(option.Id, _selectedDefaultId, StringComparison.Ordinal)
                : string.Equals(option.Id, _selectedGifId, StringComparison.Ordinal);
            border.BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(0x58, 0xE0, 0xB4)
                : Color.FromRgb(0x2C, 0x5B, 0x84));
            border.Background = new SolidColorBrush(selected
                ? Color.FromRgb(0x17, 0x3A, 0x57)
                : Color.FromRgb(0x0B, 0x1D, 0x33));
        }

        var selectedDefault = _engine.DefaultAvatars
            .FirstOrDefault(item => string.Equals(item.Id, _selectedDefaultId, StringComparison.Ordinal));
        var selectedGif = _selectedGifId is null ? null : GifName.Display(_selectedGifId);
        ApplyButton.IsEnabled = selectedDefault is not null || _selectedGifId is not null;
        if (selectedDefault is not null)
        {
            CaptionText.Text = "将切换为：默认主形象 · " +
                (selectedDefault.IsBuiltIn ? "内置默认形象" : "自定义主形象");
        }
        else if (_selectedGifId is not null)
        {
            CaptionText.Text = "将切换为：动态图 · " + selectedGif;
        }
        else
        {
            CaptionText.Text = "当前形象不在上方选项中";
        }
        CounterText.Text = "默认主形象与动态图互不影响；动态图最多启用 5 个。";

        var current = _engine.State.Avatar;
        DefaultHintText.Text = current is AvatarKind.DefaultStatic or AvatarKind.CustomImage
            ? "当前形象：默认主形象"
            : "当前形象：动态图";
        DynamicHintText.Text = current == AvatarKind.Gif
            ? "当前形象：动态图"
            : "当前形象：默认主形象";
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        (bool Ok, string Message) result;
        if (!string.IsNullOrWhiteSpace(_selectedDefaultId))
        {
            var avatar = _engine.DefaultAvatars
                .FirstOrDefault(item => string.Equals(item.Id, _selectedDefaultId, StringComparison.Ordinal));
            if (avatar is null)
            {
                result = (false, "默认主形象不存在。");
            }
            else if (avatar.IsBuiltIn)
            {
                result = _engine.SelectBuiltInDefault();
            }
            else
            {
                result = _engine.SelectCustomDefault(avatar);
            }
        }
        else if (!string.IsNullOrWhiteSpace(_selectedGifId))
        {
            result = _engine.SwitchGif(_selectedGifId);
        }
        else
        {
            result = (false, "请先选择一个形象。");
        }

        if (!result.Ok)
        {
            CounterText.Text = result.Message;
            return;
        }
        _engine.Save();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection is shared between tabs; only the hint text needs refreshing.
        if (IsLoaded) UpdateSelection();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (WindowDragHelper.IsInteractive(e.OriginalSource)) return;
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        foreach (var player in _players) player.Dispose();
        _players.Clear();
    }

    private static BitmapSource? LoadDefaultImage(DefaultAvatar avatar)
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
}
