using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhalePet.Game;

/// <summary>
/// Full-size GIF preview. Wheel zoom is anchored at the pointer and limited to
/// 50%-400%; Esc, a blank click or the close button all dismiss it.
/// </summary>
public partial class GifPreviewWindow : Window
{
    private readonly string _gifId;
    private double _imageWidth = 1;
    private double _imageHeight = 1;
    private double _scale = 1;
    private double _offsetX;
    private double _offsetY;

    public GifPreviewWindow(string gifId, string source, string? displayName = null)
    {
        _gifId = gifId;
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(displayName) ? gifId : displayName;
        SourceText.Text = source;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using (var probe = GameRuntime.Gifs?.GetDecryptedStream(_gifId))
            {
                if (probe is null) throw new InvalidOperationException("动态图不可用。");
                var decoder = new GifBitmapDecoder(probe,
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) throw new InvalidOperationException("动态图没有可显示的画面。");
                _imageWidth = Math.Max(1, decoder.Frames[0].PixelWidth);
                _imageHeight = Math.Max(1, decoder.Frames[0].PixelHeight);
            }

            ImageHit.Width = _imageWidth;
            ImageHit.Height = _imageHeight;
            PreviewGif.Width = _imageWidth;
            PreviewGif.Height = _imageHeight;
            var stream = GameRuntime.Gifs?.GetDecryptedStream(_gifId);
            if (stream is null) throw new InvalidOperationException("动态图不可用。");
            PreviewGif.SetStream(stream);

            var viewportWidth = Math.Max(200, Viewport.ActualWidth);
            var viewportHeight = Math.Max(160, Viewport.ActualHeight);
            var fit = Math.Min(1.0, Math.Min(
                (viewportWidth - 32) / _imageWidth,
                (viewportHeight - 32) / _imageHeight));
            _scale = Math.Clamp(fit, 0.5, 1.0);
            _offsetX = (viewportWidth - _imageWidth * _scale) / 2;
            _offsetY = (viewportHeight - _imageHeight * _scale) / 2;
            ApplyTransform();
        }
        catch (Exception error)
        {
            MessageBox.Show("动态图预览失败：" + error.Message, "鲸鱼娘",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    private void ApplyTransform()
    {
        ImageScale.ScaleX = _scale;
        ImageScale.ScaleY = _scale;
        ImageOffset.X = _offsetX;
        ImageOffset.Y = _offsetY;
        ZoomText.Text = $"缩放 {_scale * 100:0}%";
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var position = e.GetPosition(Viewport);
        var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        var next = Math.Clamp(_scale * factor, 0.5, 4.0);
        if (Math.Abs(next - _scale) < 0.0001) return;

        // Keep the image point under the cursor stationary.
        _offsetX = position.X - (position.X - _offsetX) * (next / _scale);
        _offsetY = position.Y - (position.Y - _offsetY) * (next / _scale);
        _scale = next;
        ApplyTransform();
        e.Handled = true;
    }

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Clicks on the image itself do not dismiss the preview.
        e.Handled = true;
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInside(source, ImageHit)) return;
        Close();
    }

    private static bool IsInside(DependencyObject source, DependencyObject ancestor)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor)) return true;
        }
        return false;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        PreviewGif.Dispose();
    }
}
