using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace WhalePet;

public partial class ScreenshotOverlayWindow : Window
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private readonly Drawing.Bitmap _screen;
    private Rect _selection;
    private Point _dragStart;
    private Rect _dragStartRect;
    private bool _dragging;
    private bool _moving;
    private int _resizeHandle = -1;

    private ScreenshotOverlayWindow(Drawing.Bitmap screen)
    {
        InitializeComponent();
        _screen = screen;

        var source = ToSource(screen);
        ScreenImage.Source = source;
        BrightImage.Source = source;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = Math.Max(1, SystemParameters.VirtualScreenWidth);
        Height = Math.Max(1, SystemParameters.VirtualScreenHeight);

        Loaded += (_, _) =>
        {
            UpdateChrome();
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

    private void OnToolbarMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var point = e.GetPosition(RootGrid);
        _dragStart = point;

        if (!_selection.IsEmpty)
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
                _dragStartRect = _selection;
                _dragging = true;
                RootGrid.CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        _moving = false;
        _resizeHandle = -1;
        _dragging = true;
        _selection = new Rect(point, point);
        RootGrid.CaptureMouse();
        UpdateChrome();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(RootGrid);
        if (!_dragging)
        {
            UpdateCursor(point);
            return;
        }

        point = ClampPoint(point);
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
        UpdateChrome();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_dragging) return;
        _dragging = false;
        _moving = false;
        _resizeHandle = -1;
        RootGrid.ReleaseMouseCapture();

        if (_selection.Width < 8 || _selection.Height < 8)
        {
            _selection = Rect.Empty;
            UpdateChrome();
        }
        e.Handled = true;
    }

    private void UpdateChrome()
    {
        var visible = !_selection.IsEmpty && _selection.Width >= 4 && _selection.Height >= 4;
        var chrome = visible ? Visibility.Visible : Visibility.Collapsed;
        SelectionBorder.Visibility = chrome;
        SizeBadge.Visibility = chrome;
        for (var i = 0; i < 8; i++)
        {
            HandleAt(i).Visibility = chrome;
        }

        ConfirmButton.IsEnabled = visible;
        if (!visible)
        {
            BrightImage.Clip = null;
            HintText.Text = "拖动选择截图区域，可拖动边缘调整";
            return;
        }

        var rect = _selection;
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;
        BrightImage.Clip = new RectangleGeometry(rect);

        var centers = HandleCenters(rect);
        for (var i = 0; i < 8; i++)
        {
            var handle = HandleAt(i);
            Canvas.SetLeft(handle, centers[i].X - handle.Width / 2);
            Canvas.SetTop(handle, centers[i].Y - handle.Height / 2);
        }

        var scaleX = _screen.Width / Math.Max(1, ActualWidth);
        var scaleY = _screen.Height / Math.Max(1, ActualHeight);
        var pixelWidth = (int)Math.Round(rect.Width * scaleX);
        var pixelHeight = (int)Math.Round(rect.Height * scaleY);
        SizeText.Text = $"{pixelWidth} × {pixelHeight}";
        var badgeTop = rect.Top > 32 ? rect.Top - 30 : rect.Bottom + 6;
        Canvas.SetLeft(SizeBadge, Math.Max(0, rect.X));
        Canvas.SetTop(SizeBadge, Math.Clamp(badgeTop, 0, Math.Max(0, ActualHeight - 28)));
        HintText.Text = "可拖动边缘调整，点击“编辑截图”继续";
    }

    private void UpdateCursor(Point point)
    {
        if (_selection.IsEmpty)
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

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (_selection.IsEmpty || _selection.Width < 4 || _selection.Height < 4) return;
        var scaleX = _screen.Width / Math.Max(1, ActualWidth);
        var scaleY = _screen.Height / Math.Max(1, ActualHeight);
        var x = Math.Clamp((int)Math.Round(_selection.X * scaleX), 0, _screen.Width - 1);
        var y = Math.Clamp((int)Math.Round(_selection.Y * scaleY), 0, _screen.Height - 1);
        var width = Math.Clamp((int)Math.Round(_selection.Width * scaleX), 1, _screen.Width - x);
        var height = Math.Clamp((int)Math.Round(_selection.Height * scaleY), 1, _screen.Height - y);
        var cropped = _screen.Clone(new Drawing.Rectangle(x, y, width, height), _screen.PixelFormat);

        var editor = new ScreenshotEditorWindow(cropped);
        editor.Show();
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            OnConfirm(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
