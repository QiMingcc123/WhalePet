using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Drawing = System.Drawing;

namespace WhalePet;

public partial class ScreenshotEditorWindow : Window
{
    private readonly Drawing.Bitmap _image;
    private readonly List<UIElement> _annotations = new();
    private string _tool = "select";
    private Color _color = Color.FromRgb(0xFF, 0x3B, 0x30);
    private Point _startPoint;
    private UIElement? _draft;
    private Line? _draftLine;
    private Polygon? _draftArrowHead;
    private bool _drawing;

    public ScreenshotEditorWindow(Drawing.Bitmap image)
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
        ToolbarBorder.Background = ThemePalette.Panel(settings.ThemeColor, 0.98, 0.22);
        FooterBorder.Background = ThemePalette.Panel(settings.ThemeColor, 0.98, 0.22);
        SaveButton.Background = ThemePalette.Brush(accent);
        SaveButton.BorderBrush = ThemePalette.Brush(accent);

        _image = image;
        EditorCanvas.Width = image.Width;
        EditorCanvas.Height = image.Height;

        var picture = new Image
        {
            Source = ToSource(image),
            Width = image.Width,
            Height = image.Height,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
        };
        EditorCanvas.Children.Add(picture);

        Width = Math.Min(1200, Math.Max(900, image.Width + 60));
        Height = Math.Min(800, Math.Max(560, image.Height + 130));
        ColorButton.Background = new SolidColorBrush(_color);
        UpdateToolButtons();

        Closed += (_, _) =>
        {
            try { _image.Dispose(); } catch { }
        };
    }

    private static BitmapSource ToSource(Drawing.Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private double StrokeThickness
    {
        get
        {
            if (!double.TryParse(ThicknessBox.Text.Trim(), out var value)) return 3;
            return Math.Clamp(value, 1, 12);
        }
    }

    private SolidColorBrush StrokeBrush => new(_color);

    private void OnToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tool }) return;
        _tool = tool;
        UpdateToolButtons();
        StatusText.Text = tool switch
        {
            "text" => "文字：点击截图位置后直接输入。",
            "line" => "直线：按住拖动绘制。",
            "arrow" => "箭头：按住拖动绘制。",
            "rect" => "方框：按住拖动绘制。",
            _ => "选择工具后可拖动绘制，或切换文字/线条/箭头/方框。",
        };
    }

    private void UpdateToolButtons()
    {
        var buttons = new (Button Button, string Tool)[]
        {
            (SelectToolButton, "select"),
            (TextToolButton, "text"),
            (LineToolButton, "line"),
            (ArrowToolButton, "arrow"),
            (RectToolButton, "rect"),
        };
        foreach (var (button, tool) in buttons)
        {
            button.Background = new SolidColorBrush(tool == _tool
                ? Color.FromRgb(0x4D, 0x6B, 0xFE)
                : Color.FromArgb(0x14, 0x8F, 0xD3, 0xFF));
            button.Foreground = tool == _tool ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xCF, 0xEA, 0xFF));
        }
    }

    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerWindow($"#{_color.R:X2}{_color.G:X2}{_color.B:X2}") { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _color = ThemePalette.Parse(dialog.SelectedColor, _color);
        ColorButton.Background = new SolidColorBrush(_color);
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var point = e.GetPosition(EditorCanvas);

        if (_tool == "text")
        {
            AddText(point);
            e.Handled = true;
            return;
        }
        if (_tool == "select") return;

        _startPoint = point;
        _drawing = true;
        EditorCanvas.CaptureMouse();

        switch (_tool)
        {
            case "line":
            {
                var line = new Line
                {
                    X1 = point.X,
                    Y1 = point.Y,
                    X2 = point.X,
                    Y2 = point.Y,
                    Stroke = StrokeBrush,
                    StrokeThickness = StrokeThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                _draftLine = line;
                _draft = line;
                break;
            }
            case "arrow":
            {
                var line = new Line
                {
                    X1 = point.X,
                    Y1 = point.Y,
                    X2 = point.X,
                    Y2 = point.Y,
                    Stroke = StrokeBrush,
                    StrokeThickness = StrokeThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                };
                var head = new Polygon { Fill = StrokeBrush, IsHitTestVisible = false };
                var group = new Canvas
                {
                    Width = EditorCanvas.Width,
                    Height = EditorCanvas.Height,
                    IsHitTestVisible = false,
                };
                group.Children.Add(line);
                group.Children.Add(head);
                _draftLine = line;
                _draftArrowHead = head;
                _draft = group;
                break;
            }
            default:
            {
                var rect = new Rectangle
                {
                    Stroke = StrokeBrush,
                    StrokeThickness = StrokeThickness,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(rect, point.X);
                Canvas.SetTop(rect, point.Y);
                rect.Width = 0;
                rect.Height = 0;
                _draft = rect;
                break;
            }
        }

        if (_draft is not null) EditorCanvas.Children.Add(_draft);
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing || _draft is null) return;
        var point = e.GetPosition(EditorCanvas);
        var left = Math.Min(_startPoint.X, point.X);
        var top = Math.Min(_startPoint.Y, point.Y);
        var width = Math.Abs(point.X - _startPoint.X);
        var height = Math.Abs(point.Y - _startPoint.Y);

        if (_draft is Rectangle rect)
        {
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            rect.Width = width;
            rect.Height = height;
        }
        else if (_draftLine is not null)
        {
            _draftLine.X2 = point.X;
            _draftLine.Y2 = point.Y;
            if (_draftArrowHead is not null)
            {
                var angle = Math.Atan2(point.Y - _startPoint.Y, point.X - _startPoint.X);
                var size = Math.Max(9, StrokeThickness * 3.2);
                var p1 = new Point(
                    point.X - size * Math.Cos(angle - Math.PI / 6),
                    point.Y - size * Math.Sin(angle - Math.PI / 6));
                var p2 = new Point(
                    point.X - size * Math.Cos(angle + Math.PI / 6),
                    point.Y - size * Math.Sin(angle + Math.PI / 6));
                _draftArrowHead.Points = new PointCollection { point, p1, p2 };
            }
        }

        e.Handled = true;
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing || _draft is null) return;
        _drawing = false;
        EditorCanvas.ReleaseMouseCapture();

        var tooSmall = _draft switch
        {
            Rectangle rect => rect.Width < 3 && rect.Height < 3,
            Line line => Math.Abs(line.X2 - line.X1) < 3 && Math.Abs(line.Y2 - line.Y1) < 3,
            _ => false,
        };
        if (tooSmall)
        {
            EditorCanvas.Children.Remove(_draft);
        }
        else
        {
            _annotations.Add(_draft);
        }

        _draft = null;
        _draftLine = null;
        _draftArrowHead = null;
        e.Handled = true;
    }

    private void AddText(Point point)
    {
        var textBox = new TextBox
        {
            Width = 220,
            MinHeight = 32,
            FontSize = 20,
            Foreground = StrokeBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            AcceptsReturn = false,
            TextWrapping = TextWrapping.NoWrap,
            Text = "",
        };
        Canvas.SetLeft(textBox, point.X);
        Canvas.SetTop(textBox, point.Y);
        EditorCanvas.Children.Add(textBox);
        _annotations.Add(textBox);
        textBox.Focus();
        textBox.LostKeyboardFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                EditorCanvas.Children.Remove(textBox);
                _annotations.Remove(textBox);
            }
        };
        StatusText.Text = "输入文字后点击画布其他位置即可。";
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_annotations.Count == 0) return;
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        EditorCanvas.Children.Remove(last);
        StatusText.Text = "已撤销上一步。";
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        foreach (var item in _annotations.ToList())
        {
            EditorCanvas.Children.Remove(item);
        }
        _annotations.Clear();
        StatusText.Text = "已清空标注。";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存截图",
            Filter = "PNG 图片|*.png",
            FileName = $"鲸鱼娘截图_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            EditorCanvas.UpdateLayout();
            var width = Math.Max(1, (int)Math.Round(EditorCanvas.Width));
            var height = Math.Max(1, (int)Math.Round(EditorCanvas.Height));

            // The canvas lives inside a ScrollViewer; cancel its layout offset so
            // the rendered bitmap starts at the canvas's own origin.
            var origin = EditorCanvas.TranslatePoint(new Point(0, 0), this);
            var previousTransform = EditorCanvas.RenderTransform;
            EditorCanvas.RenderTransform = new TranslateTransform(-origin.X, -origin.Y);
            EditorCanvas.UpdateLayout();
            try
            {
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(EditorCanvas);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
            }
            finally
            {
                EditorCanvas.RenderTransform = previousTransform;
            }
            StatusText.Text = "已保存：" + dialog.FileName;
        }
        catch (Exception error)
        {
            StatusText.Text = "保存失败：" + error.Message;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
