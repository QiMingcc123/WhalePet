using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WhalePet;

/// <summary>Shared desktop-panel placement: virtual-screen clamp, edge snap and display changes.</summary>
public static class PanelPlacementHelper
{
    private const uint MonitorDefaultToNearest = 2;
    private const int WmDisplayChange = 0x007E;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    private static readonly Dictionary<Window, (HwndSource Source, HwndSourceHook Hook)> DisplayHooks = new();

    public static void RestorePosition(Window window, double? left, double? top)
    {
        if (left is not { } savedLeft || top is not { } savedTop ||
            !double.IsFinite(savedLeft) || !double.IsFinite(savedTop))
        {
            CenterOnPrimary(window);
            return;
        }

        var virtualArea = VirtualArea();
        var candidate = new Rect(savedLeft, savedTop, window.Width, window.Height);
        var visible = Rect.Intersect(candidate, virtualArea);
        if (visible.IsEmpty || visible.Width < 60 || visible.Height < 40)
        {
            CenterOnPrimary(window);
            return;
        }

        window.Left = savedLeft;
        window.Top = savedTop;
        ClampToVirtual(window);
    }

    public static void CenterOnPrimary(Window window)
    {
        var area = SystemParameters.WorkArea;
        window.Left = window.Width > area.Width
            ? area.Left
            : area.Left + Math.Max(0, (area.Width - window.Width) / 2);
        window.Top = window.Height > area.Height
            ? area.Top
            : area.Top + Math.Max(0, (area.Height - window.Height) / 2);
    }

    /// <summary>Reassert topmost z-order so a reading panel can cover a taskbar.</summary>
    public static void BringToTop(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
        }
        catch
        {
            // Topmost is best effort; WPF's Topmost property remains active.
        }
    }

    public static void ClampToVirtual(Window window)
    {
        var area = VirtualArea();
        var maxLeft = Math.Max(area.Left, area.Right - window.Width);
        var maxTop = Math.Max(area.Top, area.Bottom - window.Height);
        window.Left = Math.Clamp(window.Left, area.Left, maxLeft);
        window.Top = Math.Clamp(window.Top, area.Top, maxTop);
    }

    public static void SnapToEdge(Window window, double threshold = 18) =>
        SnapToEdge(window, threshold, includeTaskbar: false);

    public static void SnapToEdge(Window window, double threshold, bool includeTaskbar)
    {
        if (!GetCursorPos(out var cursor)) return;
        var area = includeTaskbar
            ? MonitorBoundsAtPoint(cursor, window)
            : WorkAreaAtPoint(cursor, window);
        if (cursor.X - area.Left <= threshold) window.Left = area.Left;
        else if (area.Right - cursor.X <= threshold)
            window.Left = area.Right - window.Width;

        if (cursor.Y - area.Top <= threshold) window.Top = area.Top;
        else if (area.Bottom - cursor.Y <= threshold)
            window.Top = area.Bottom - window.Height;

        ClampToVirtual(window);
    }

    public static void ReturnToPrimary(Window window) =>
        ReturnToPrimary(window, includeTaskbar: false);

    public static void ReturnToPrimary(Window window, bool includeTaskbar)
    {
        var area = includeTaskbar
            ? MonitorBoundsAtPoint(new POINT { X = 0, Y = 0 }, window)
            : SystemParameters.WorkArea;
        if (window.Width > area.Width) window.Width = Math.Max(300, area.Width);
        if (window.Height > area.Height) window.Height = Math.Max(300, area.Height);
        window.Left = Math.Clamp(window.Left, area.Left, Math.Max(area.Left, area.Right - window.Width));
        window.Top = Math.Clamp(window.Top, area.Top, Math.Max(area.Top, area.Bottom - window.Height));
    }

    public static void AttachDisplayWatcher(Window window, Action changed) =>
        AttachDisplayWatcher(window, changed, includeTaskbar: false);

    public static void AttachDisplayWatcher(Window window, Action changed, bool includeTaskbar)
    {
        void Attach()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var source = HwndSource.FromHwnd(handle);
            if (source is null) return;
            HwndSourceHook? hook = null;
            hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg != WmDisplayChange) return IntPtr.Zero;
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ReturnToPrimary(window, includeTaskbar);
                    changed();
                }));
                return IntPtr.Zero;
            };
            source.AddHook(hook);
            DisplayHooks[window] = (source, hook);
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Attach();
        else window.SourceInitialized += (_, _) => Attach();
        window.Closed += (_, _) =>
        {
            if (!DisplayHooks.TryGetValue(window, out var entry)) return;
            entry.Source.RemoveHook(entry.Hook);
            DisplayHooks.Remove(window);
        };
    }

    private static Rect VirtualArea() => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        Math.Max(1, SystemParameters.VirtualScreenWidth),
        Math.Max(1, SystemParameters.VirtualScreenHeight));

    private static Rect WorkAreaAtPoint(POINT point, Window window)
    {
        try
        {
            var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
            return MonitorArea(monitor, window, workArea: true);
        }
        catch
        {
            return SystemParameters.WorkArea;
        }
    }

    private static Rect MonitorBoundsAtPoint(POINT point, Window window)
    {
        try
        {
            var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
            return MonitorArea(monitor, window, workArea: false);
        }
        catch
        {
            return new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                Math.Max(1, SystemParameters.VirtualScreenWidth),
                Math.Max(1, SystemParameters.VirtualScreenHeight));
        }
    }

    private static Rect MonitorArea(IntPtr monitor, Window window, bool workArea)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var scaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
            var scaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;
            var source = workArea ? info.rcWork : info.rcMonitor;
            return new Rect(
                source.Left / scaleX,
                source.Top / scaleY,
                Math.Max(1, (source.Right - source.Left) / scaleX),
                Math.Max(1, (source.Bottom - source.Top) / scaleY));
        }
        return workArea
            ? SystemParameters.WorkArea
            : new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                Math.Max(1, SystemParameters.VirtualScreenWidth),
                Math.Max(1, SystemParameters.VirtualScreenHeight));
    }
}
