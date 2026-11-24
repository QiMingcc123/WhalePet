using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WhalePet.Game;

/// <summary>
/// Minimal GIF player. It accepts a Stream (never a path), decodes every frame
/// with BitmapCacheOption.OnLoad and drives playback with a DispatcherTimer.
/// </summary>
public partial class GifPlayerControl : UserControl, IDisposable
{
    private readonly DispatcherTimer _timer;
    private BitmapFrame[] _frames = Array.Empty<BitmapFrame>();
    private TimeSpan[] _delays = Array.Empty<TimeSpan>();
    private Stream? _stream;
    private int _index;
    private bool _disposed;

    public GifPlayerControl()
    {
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += OnFrameTick;
    }

    public bool HasFrames => _frames.Length > 0;

    public void SetStream(Stream stream)
    {
        if (_disposed) return;
        Clear();
        _stream = stream;
        try
        {
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            _frames = decoder.Frames.Cast<BitmapFrame>().ToArray();
            if (_frames.Length == 0) return;

            _delays = _frames.Select(ReadDelay).ToArray();
            _index = 0;
            FrameImage.Source = _frames[0];
            _timer.Interval = _delays[0];
            _timer.Start();
        }
        catch
        {
            Clear();
            throw;
        }
    }

    public void Clear()
    {
        _timer.Stop();
        _frames = Array.Empty<BitmapFrame>();
        _delays = Array.Empty<TimeSpan>();
        _index = 0;
        FrameImage.Source = null;
        try { _stream?.Dispose(); } catch { }
        _stream = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        _timer.Tick -= OnFrameTick;
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        if (_frames.Length == 0) return;
        _index = (_index + 1) % _frames.Length;
        FrameImage.Source = _frames[_index];
        _timer.Interval = _delays[_index];
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_frames.Length > 1 && !_timer.IsEnabled) _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private static TimeSpan ReadDelay(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata metadata)
            {
                var delay = metadata.GetQuery("/grctlext/Delay");
                if (delay is ushort hundredths && hundredths > 0)
                {
                    return TimeSpan.FromMilliseconds(Math.Clamp(hundredths * 10, 20, 1000));
                }
            }
        }
        catch
        {
            // Some GIFs have malformed metadata; use a sane default.
        }
        return TimeSpan.FromMilliseconds(100);
    }
}
