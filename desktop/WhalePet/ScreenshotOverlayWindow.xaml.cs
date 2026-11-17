using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;

namespace WhalePet;

public partial class ScreenshotOverlayWindow : Window
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private readonly Drawing.Bitmap _screen;
    private readonly BitmapSource _source;
    private readonly List<UIElement> _annotations = new();

    private Rect _selection;
    private Point _dragStart;
    private Rect _dragStartRect;
    private bool _dragging;
    private bool _moving;
    private int _resizeHandle = -1;

    private string _tool = "select";
    private Color _color = Color.FromRgb(0xFF, 0x3B, 0x30);
    private Color _accent = Color.FromRgb(0x4D, 0x6B, 0xFE);
    private double _arrowScale = 1;
    private bool _drawingAnnotation;
    private Point _drawStart;
    private UIElement? _draft;
    private Line? _draftLine;
    private Polygon? _draftArrowHead;

    private ScreenshotOverlayWindow(Drawing.Bitmap screen)
    {
        InitializeComponent();
        _screen = screen;
        _source = ToSource(screen);
        ScreenImage.Source = _source;
        BrightImage.Source = _source;

        var settings = SettingsStore.Load();
        _accent = ThemePalette.Parse(settings.ThemeColor, _accent);
        CopyButton.Background = ThemePalette.Brush(_accent);
        CopyButton.BorderBrush = ThemePalette.Brush(_accent);

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = Math.Max(1, SystemParameters.VirtualScreenWidth);
        Height = Math.Max(1, SystemParameters.VirtualScreenHeight);

        Loaded += (_, _) =>
        {
            UpdateChrome();
            UpdateToolButtons();
            ColorButton.Background = new SolidColorBrush(_color);
            Activate();
            Focus();
        };
        Closed += (_, _) =>
        {
            try { _screen.Dispose(); } catch { }
        };
    }

    public static ScreenshotOverlayWindow Create()
    {
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var width = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        var height = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));

        var bitmap = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size, Drawing.CopyPixelOperation.SourceCopy);
        }
        return new ScreenshotOverlayWindow(bitmap);
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

    private bool RegionLocked => _annotations.Count > 0;

    private void OnToolbarMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var point = e.GetPosition(RootGrid);

        if (!_selection.IsEmpty)
        {
            if (_tool != "select")
            {
                if (!_selection.Contains(point))
                {
                    HintText.Text = "请在选区内标注；需要重新选择区域请点“重选”。";
                    return;
                }

                if (_tool == "text")
                {
                    AddText(point);
                }
                else
                {
                    BeginAnnotation(point);
                }
                e.Handled = true;
                return;
            }

            if (!RegionLocked)
            {
                var handle = HitHandle(point);
                if (handle >= 0)
                {
                    _resizeHandle = handle;
                    _dragStartRect = _selection;
                    _dragging = true;
                    RootGrid.CaptureMouse();
                    e.Handled = true;
                    return;
                }
                if (_selection.Contains(point))
                {
                    _moving = true;
                    _dragStart = point;
                    _dragStartRect = _selection;
                    _dragging = true;
                    RootGrid.CaptureMouse();
                    e.Handled = true;
                    return;
                }
            }
            else if (_tool == "select" && HitHandle(point) >= 0)
            {
                _resizeHandle = HitHandle(point);
                _dragStartRect = _selection;
                _dragging = true;
                RootGrid.CaptureMouse();
                e.Handled = true;
                return;
            }
            else if (_tool == "select" && _selection.Contains(point))
            {
                _moving = true;
                _dragStart = point;
                _dragStartRect = _selection;
                _dragging = true;
                RootGrid.CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        if (RegionLocked) return;

        _moving = false;
        _resizeHandle = -1;
        _dragging = true;
        _dragStart = point;
        _selection = new Rect(point, point);
        RootGrid.CaptureMouse();
        UpdateChrome();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(RootGrid);
        if (_drawingAnnotation)
        {
            UpdateAnnotation(point);
            e.Handled = true;
            return;
        }

        if (!_dragging)
        {
            UpdateCursor(point);
            return;
        }

        point = ClampPoint(point);
        var oldSelection = _selection;
        if (_moving)
        {
            var dx = point.X - _dragStart.X;
            var dy = point.Y - _dragStart.Y;
            var left = Math.Clamp(_dragStartRect.X + dx, 0, Math.Max(0, ActualWidth - _dragStartRect.Width));
            var top = Math.Clamp(_dragStartRect.Y + dy, 0, Math.Max(0, ActualHeight - _dragStartRect.Height));
            _selection = new Rect(left, top, _dragStartRect.Width, _dragStartRect.Height);
        }
        else if (_resizeHandle >= 0)
        {
            _selection = ResizeRect(_dragStartRect, _resizeHandle, point);
        }
        else
        {
            var rect = new Rect(_dragStart, point);
            _selection = rect.Width < 0 || rect.Height < 0
                ? new Rect(Math.Min(rect.Left, rect.Right), Math.Min(rect.Top, rect.Bottom),
                    Math.Abs(rect.Width), Math.Abs(rect.Height))
                : rect;
        }

        _selection = ClampRect(_selection);
        if (_moving && RegionLocked && !_selection.IsEmpty)
        {
            TranslateAnnotations(_selection.X - oldSelection.X, _selection.Y - oldSelection.Y);
        }
        else if (_resizeHandle >= 0 && RegionLocked
                 && oldSelection.Width > 1 && oldSelection.Height > 1
                 && _selection.Width > 1 && _selection.Height > 1)
        {
            ScaleAnnotations(oldSelection, _selection);
        }
        UpdateChrome();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (_drawingAnnotation)
        {
            FinishAnnotation();
            e.Handled = true;
            return;
        }

        if (!_dragging) return;
        _dragging = false;
        _moving = false;
        _resizeHandle = -1;
        RootGrid.ReleaseMouseCapture();

        if (_selection.Width < 8 || _selection.Height < 8)
        {
            _selection = Rect.Empty;
        }
        UpdateChrome();
        e.Handled = true;
    }

    private void UpdateChrome()
    {
        var visible = !_selection.IsEmpty && _selection.Width >= 4 && _selection.Height >= 4;
        var chrome = visible ? Visibility.Visible : Visibility.Collapsed;
        SelectionBorder.Visibility = chrome;
        SizeBadge.Visibility = chrome;
        for (var i = 0; i < 8; i++) HandleAt(i).Visibility = chrome;

        CopyButton.IsEnabled = visible;
        EditTools.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        if (!visible)
        {
            BrightImage.Clip = null;
            AnnotationCanvas.Clip = null;
            HintText.Text = "拖动选择截图区域；松开后可调整区域或直接标注";
            return;
        }

        var rect = _selection;
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;
        BrightImage.Clip = new RectangleGeometry(rect);
        AnnotationCanvas.Clip = new RectangleGeometry(rect);

        var centers = HandleCenters(rect);
        for (var i = 0; i < 8; i++)
        {
            var handle = HandleAt(i);
            Canvas.SetLeft(handle, centers[i].X - handle.Width / 2);
            Canvas.SetTop(handle, centers[i].Y - handle.Height / 2);
        }

        var (pixelWidth, pixelHeight) = GetPixelSize(rect);
        SizeText.Text = $"{pixelWidth} × {pixelHeight}";
        var badgeTop = rect.Top > 32 ? rect.Top - 30 : rect.Bottom + 6;
        Canvas.SetLeft(SizeBadge, Math.Max(0, rect.X));
        Canvas.SetTop(SizeBadge, Math.Clamp(badgeTop, 0, Math.Max(0, ActualHeight - 28)));

        HintText.Text = RegionLocked
            ? "已添加标注；仍可拖动边缘缩放或拖动选区移动。"
            : "拖动边缘调整区域；选择方框/圆/直线/箭头/文字后在选区内标注。";
        PositionToolbar();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!_selection.IsEmpty) PositionToolbar();
        }));
    }

    private void PositionToolbar()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        Toolbar.Measure(new Size(Math.Max(1, ActualWidth), double.PositiveInfinity));
        var desired = Toolbar.DesiredSize;
        var width = Math.Min(
            Toolbar.ActualWidth > 0 ? Toolbar.ActualWidth : desired.Width,
            Math.Max(1, ActualWidth - 16));
        var height = Toolbar.ActualHeight > 0 ? Toolbar.ActualHeight : Math.Min(desired.Height, 120);
        if (height < 10) height = 80;
        double left;
        double top;

        if (_selection.IsEmpty)
        {
            left = Math.Max(8, (ActualWidth - width) / 2);
            top = 12;
        }
        else
        {
            left = Math.Clamp(
                _selection.X + (_selection.Width - width) / 2,
                8,
                Math.Max(8, ActualWidth - width - 8));

            var below = _selection.Bottom + 20;
            var above = _selection.Top - 20 - height;
            if (below + height <= ActualHeight - 8)
            {
                top = below;
            }
            else if (above >= 8)
            {
                top = above;
            }
            else
            {
                // Extremely tall selection: use the side with more room.
                top = Math.Clamp(ActualHeight - height - 8, 8, Math.Max(8, ActualHeight - height));
            }
        }

        Toolbar.HorizontalAlignment = HorizontalAlignment.Left;
        Toolbar.VerticalAlignment = VerticalAlignment.Top;
        Toolbar.Margin = new Thickness(left, top, 0, 0);
    }

    private void TranslateAnnotations(double dx, double dy)
    {
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) return;
        foreach (var item in _annotations)
        {
            var transform = item.RenderTransform as TranslateTransform ?? new TranslateTransform();
            transform.X += dx;
            transform.Y += dy;
            item.RenderTransform = transform;
        }
    }

    private void ScaleAnnotations(Rect oldRect, Rect newRect)
    {
        var scaleX = newRect.Width / Math.Max(1, oldRect.Width);
        var scaleY = newRect.Height / Math.Max(1, oldRect.Height);
        if (Math.Abs(scaleX - 1) < 0.001 && Math.Abs(scaleY - 1) < 0.001) return;

        var matrix = new Matrix(
            scaleX, 0,
            0, scaleY,
            newRect.X - oldRect.X * scaleX,
            newRect.Y - oldRect.Y * scaleY);
        var transform = new MatrixTransform(matrix);
        foreach (var item in _annotations)
        {
            var group = new TransformGroup();
            if (item.RenderTransform is { } existing && existing != Transform.Identity)
            {
                group.Children.Add(existing);
            }
            group.Children.Add(transform);
            item.RenderTransform = group;
        }
    }

    private void UpdateCursor(Point point)
    {
        if (_selection.IsEmpty)
        {
            RootGrid.Cursor = Cursors.Cross;
            return;
        }

        if (_tool == "text" && _selection.Contains(point))
        {
            RootGrid.Cursor = Cursors.IBeam;
            return;
        }
        if (_tool != "select" && _selection.Contains(point))
        {
            RootGrid.Cursor = Cursors.Cross;
            return;
        }

        var handle = HitHandle(point);
        RootGrid.Cursor = handle switch
        {
            0 or 4 => Cursors.SizeNWSE,
            2 or 6 => Cursors.SizeNESW,
            1 or 5 => Cursors.SizeNS,
            3 or 7 => Cursors.SizeWE,
            _ => _selection.Contains(point) ? Cursors.SizeAll : Cursors.Cross,
        };
    }

    private int HitHandle(Point point)
    {
        if (_selection.IsEmpty) return -1;
        var centers = HandleCenters(_selection);
        for (var i = 0; i < centers.Length; i++)
        {
            if (Math.Abs(point.X - centers[i].X) <= 8 && Math.Abs(point.Y - centers[i].Y) <= 8) return i;
        }
        return -1;
    }

    private static Point[] HandleCenters(Rect rect) => new[]
    {
        new Point(rect.Left, rect.Top),
        new Point(rect.Left + rect.Width / 2, rect.Top),
        new Point(rect.Right, rect.Top),
        new Point(rect.Right, rect.Top + rect.Height / 2),
        new Point(rect.Right, rect.Bottom),
        new Point(rect.Left + rect.Width / 2, rect.Bottom),
        new Point(rect.Left, rect.Bottom),
        new Point(rect.Left, rect.Top + rect.Height / 2),
    };

    private Border HandleAt(int index) => index switch
    {
        0 => Handle0,
        1 => Handle1,
        2 => Handle2,
        3 => Handle3,
        4 => Handle4,
        5 => Handle5,
        6 => Handle6,
        _ => Handle7,
    };

    private Rect ResizeRect(Rect rect, int handle, Point point)
    {
        double left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom;
        switch (handle)
        {
            case 0: left = point.X; top = point.Y; break;
            case 1: top = point.Y; break;
            case 2: right = point.X; top = point.Y; break;
            case 3: right = point.X; break;
            case 4: right = point.X; bottom = point.Y; break;
            case 5: bottom = point.Y; break;
            case 6: left = point.X; bottom = point.Y; break;
            case 7: left = point.X; break;
        }
        if (right < left) (left, right) = (right, left);
        if (bottom < top) (top, bottom) = (bottom, top);
        return new Rect(left, top, right - left, bottom - top);
    }

    private Rect ClampRect(Rect rect)
    {
        var left = Math.Clamp(rect.Left, 0, Math.Max(0, ActualWidth));
        var top = Math.Clamp(rect.Top, 0, Math.Max(0, ActualHeight));
        var right = Math.Clamp(rect.Right, 0, Math.Max(0, ActualWidth));
        var bottom = Math.Clamp(rect.Bottom, 0, Math.Max(0, ActualHeight));
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, Math.Max(0, ActualWidth)),
        Math.Clamp(point.Y, 0, Math.Max(0, ActualHeight)));

    private void OnToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tool }) return;
        _tool = tool;
        UpdateToolButtons();
        RootGrid.Cursor = tool == "text" ? Cursors.IBeam : Cursors.Cross;
        HintText.Text = tool switch
        {
            "text" => "文字：点击选区后输入，右侧可调整字号。",
            "rect" => "方框：按住拖动绘制。",
            "ellipse" => "圆：按住拖动绘制椭圆。",
            "line" => "直线：按住拖动绘制。",
            "arrow" => "箭头：按住拖动绘制，可选择小/中/大。",
            _ => RegionLocked ? "已添加标注；仍可拖动边缘缩放或拖动选区移动。" : "可拖动边缘调整区域。",
        };
    }

    private void OnArrowSizeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        _arrowScale = tag switch
        {
            "arrow-s" => 0.65,
            "arrow-l" => 1.55,
            _ => 1,
        };
        UpdateToolButtons();
    }

    private void UpdateToolButtons()
    {
        var buttons = new (Button Button, string Tool)[]
        {
            (SelectToolButton, "select"),
            (RectToolButton, "rect"),
            (EllipseToolButton, "ellipse"),
            (LineToolButton, "line"),
            (ArrowToolButton, "arrow"),
            (TextToolButton, "text"),
        };
        foreach (var (button, tool) in buttons)
        {
            button.Background = new SolidColorBrush(tool == _tool
                ? _accent
                : Color.FromArgb(0x14, 0x8F, 0xD3, 0xFF));
            button.Foreground = tool == _tool
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(0xCF, 0xEA, 0xFF));
        }

        var arrowButtons = new (Button Button, string Tag)[]
        {
            (ArrowSmallButton, "arrow-s"),
            (ArrowMediumButton, "arrow-m"),
            (ArrowLargeButton, "arrow-l"),
        };
        foreach (var (button, tag) in arrowButtons)
        {
            var selected = _arrowScale switch
            {
                <= 0.8 => "arrow-s",
                >= 1.3 => "arrow-l",
                _ => "arrow-m",
            };
            button.Background = new SolidColorBrush(tag == selected
                ? _accent
                : Color.FromArgb(0x14, 0x8F, 0xD3, 0xFF));
            button.Foreground = tag == selected
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(0xCF, 0xEA, 0xFF));
        }
    }

    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerWindow($"#{_color.R:X2}{_color.G:X2}{_color.B:X2}") { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _color = ThemePalette.Parse(dialog.SelectedColor, _color);
        ColorButton.Background = new SolidColorBrush(_color);
    }

    private double StrokeThickness
    {
        get
        {
            if (!double.TryParse(ThicknessBox.Text.Trim(), out var value)) return 3;
            return Math.Clamp(value, 1, 12);
        }
    }

    private double TextFontSize
    {
        get
        {
            if (!double.TryParse(FontSizeBox.Text.Trim(), out var value)) return 20;
            return Math.Clamp(value, 10, 72);
        }
    }

    private SolidColorBrush StrokeBrush => new(_color);

    private void BeginAnnotation(Point point)
    {
        point = ClampToSelection(point);
        _drawStart = point;
        _drawingAnnotation = true;
        RootGrid.CaptureMouse();

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
                    Width = AnnotationCanvas.ActualWidth,
                    Height = AnnotationCanvas.ActualHeight,
                    IsHitTestVisible = false,
                };
                group.Children.Add(line);
                group.Children.Add(head);
                _draftLine = line;
                _draftArrowHead = head;
                _draft = group;
                break;
            }
            case "ellipse":
            {
                var ellipse = new Ellipse
                {
                    Stroke = StrokeBrush,
                    StrokeThickness = StrokeThickness,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(ellipse, point.X);
                Canvas.SetTop(ellipse, point.Y);
                _draft = ellipse;
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
                _draft = rect;
                break;
            }
        }

        if (_draft is not null) AnnotationCanvas.Children.Add(_draft);
    }

    private void UpdateAnnotation(Point point)
    {
        if (_draft is null) return;
        point = ClampToSelection(point);
        var left = Math.Min(_drawStart.X, point.X);
        var top = Math.Min(_drawStart.Y, point.Y);
        var width = Math.Abs(point.X - _drawStart.X);
        var height = Math.Abs(point.Y - _drawStart.Y);

        switch (_draft)
        {
            case Rectangle rect:
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                rect.Width = width;
                rect.Height = height;
                break;
            case Ellipse ellipse:
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
                ellipse.Width = width;
                ellipse.Height = height;
                break;
            default:
                if (_draftLine is null) break;
                _draftLine.X2 = point.X;
                _draftLine.Y2 = point.Y;
                if (_draftArrowHead is not null)
                {
                    var angle = Math.Atan2(point.Y - _drawStart.Y, point.X - _drawStart.X);
                    var size = Math.Max(9, StrokeThickness * 3.2) * _arrowScale;
                    var p1 = new Point(
                        point.X - size * Math.Cos(angle - Math.PI / 6),
                        point.Y - size * Math.Sin(angle - Math.PI / 6));
                    var p2 = new Point(
                        point.X - size * Math.Cos(angle + Math.PI / 6),
                        point.Y - size * Math.Sin(angle + Math.PI / 6));
                    _draftArrowHead.Points = new PointCollection { point, p1, p2 };
                }
                break;
        }
    }

    private void FinishAnnotation()
    {
        _drawingAnnotation = false;
        RootGrid.ReleaseMouseCapture();
        if (_draft is null) return;

        var tooSmall = _draft switch
        {
            Rectangle rect => rect.Width < 3 && rect.Height < 3,
            Ellipse ellipse => ellipse.Width < 3 && ellipse.Height < 3,
            Line line => Math.Abs(line.X2 - line.X1) < 3 && Math.Abs(line.Y2 - line.Y1) < 3,
            _ => false,
        };
        if (tooSmall)
        {
            AnnotationCanvas.Children.Remove(_draft);
        }
        else
        {
            _annotations.Add(_draft);
        }

        _draft = null;
        _draftLine = null;
        _draftArrowHead = null;
        UpdateChrome();
    }

    private void AddText(Point point)
    {
        point = ClampToSelection(point);
        var textBox = new TextBox
        {
            Width = Math.Max(140, Math.Min(320, _selection.Width - (point.X - _selection.X) - 4)),
            MinHeight = TextFontSize + 10,
            FontSize = TextFontSize,
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            Foreground = StrokeBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(1),
            AcceptsReturn = false,
            TextWrapping = TextWrapping.NoWrap,
            Text = "",
        };
        Canvas.SetLeft(textBox, point.X);
        Canvas.SetTop(textBox, point.Y);
        AnnotationCanvas.Children.Add(textBox);
        _annotations.Add(textBox);
        textBox.Focus();
        textBox.LostKeyboardFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                AnnotationCanvas.Children.Remove(textBox);
                _annotations.Remove(textBox);
            }
            UpdateChrome();
        };
        HintText.Text = "输入文字后点击选区其他位置即可；字号可在工具栏调整。";
    }

    private Point ClampToSelection(Point point) => new(
        Math.Clamp(point.X, _selection.Left, _selection.Right),
        Math.Clamp(point.Y, _selection.Top, _selection.Bottom));

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_annotations.Count == 0) return;
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        AnnotationCanvas.Children.Remove(last);
        UpdateChrome();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        foreach (var item in _annotations.ToList())
        {
            AnnotationCanvas.Children.Remove(item);
        }
        _annotations.Clear();
        UpdateChrome();
    }

    private void OnReselect(object sender, RoutedEventArgs e)
    {
        OnClear(this, new RoutedEventArgs());
        _selection = Rect.Empty;
        _tool = "select";
        UpdateToolButtons();
        UpdateChrome();
    }

    private (int X, int Y, int Width, int Height) GetPixelRect()
    {
        var scaleX = _screen.Width / Math.Max(1, ActualWidth);
        var scaleY = _screen.Height / Math.Max(1, ActualHeight);
        var x = Math.Clamp((int)Math.Round(_selection.X * scaleX), 0, _screen.Width - 1);
        var y = Math.Clamp((int)Math.Round(_selection.Y * scaleY), 0, _screen.Height - 1);
        var width = Math.Clamp((int)Math.Round(_selection.Width * scaleX), 1, _screen.Width - x);
        var height = Math.Clamp((int)Math.Round(_selection.Height * scaleY), 1, _screen.Height - y);
        return (x, y, width, height);
    }

    private (int Width, int Height) GetPixelSize(Rect rect)
    {
        var scaleX = _screen.Width / Math.Max(1, ActualWidth);
        var scaleY = _screen.Height / Math.Max(1, ActualHeight);
        return (
            Math.Max(1, (int)Math.Round(rect.Width * scaleX)),
            Math.Max(1, (int)Math.Round(rect.Height * scaleY)));
    }

    private BitmapSource RenderSelection()
    {
        var (x, y, width, height) = GetPixelRect();
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var crop = new CroppedBitmap(_source, new Int32Rect(x, y, width, height));
            context.DrawImage(crop, new Rect(0, 0, width, height));

            if (_annotations.Count > 0)
            {
                var previousClip = AnnotationCanvas.Clip;
                AnnotationCanvas.Clip = null;
                try
                {
                    var brush = new VisualBrush(AnnotationCanvas)
                    {
                        ViewboxUnits = BrushMappingMode.Absolute,
                        Viewbox = _selection,
                        Stretch = Stretch.Fill,
                        AlignmentX = AlignmentX.Left,
                        AlignmentY = AlignmentY.Top,
                    };
                    context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
                }
                finally
                {
                    AnnotationCanvas.Clip = previousClip;
                }
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static bool TrySetClipboardImage(BitmapSource bitmap)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.SetImage(bitmap);
                return true;
            }
            catch
            {
                Thread.Sleep(40);
            }
        }
        return false;
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_selection.IsEmpty) return;
        var bitmap = RenderSelection();
        if (TrySetClipboardImage(bitmap))
        {
            Close();
        }
        else
        {
            HintText.Text = "复制失败：剪贴板可能被其他程序占用，请点“保存”保存到设备。";
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_selection.IsEmpty) return;
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
            var bitmap = RenderSelection();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            Close();
        }
        catch (Exception error)
        {
            HintText.Text = "保存失败：" + error.Message;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            OnCopy(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnCopy(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnSave(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);


}
