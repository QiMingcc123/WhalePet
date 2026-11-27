using System.Runtime.InteropServices;
using System.Windows;

namespace WhalePet;

/// <summary>
/// Win32 monitor helpers used by the Live2D move/snap path. Every calculation
/// is based on the monitor that currently contains the window, never on the
/// primary screen, so a window on a secondary display snaps to that display.
/// </summary>
public static class MonitorPlacement
{
    public const int SnapDistancePx = 15;

    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorDefaultToPrimary = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip,
        MonitorEnumProc callback, IntPtr data);

    public static bool TryGetWindowRect(IntPtr hwnd, out RECT rect)
    {
        rect = default;
        return hwnd != IntPtr.Zero && GetWindowRect(hwnd, out rect) &&
               rect.Width > 0 && rect.Height > 0;
    }

    public static RECT WorkAreaForWindow(IntPtr hwnd)
    {
        return WorkAreaForMonitor(MonitorFromWindow(hwnd, MonitorDefaultToNearest));
    }

    public static RECT WorkAreaForRect(RECT rect)
    {
        return WorkAreaForMonitor(MonitorFromRect(ref rect, MonitorDefaultToNearest));
    }

    public static RECT PrimaryWorkArea()
    {
        return WorkAreaForMonitor(MonitorFromPoint(new POINT(), MonitorDefaultToPrimary));
    }

    private static RECT WorkAreaForMonitor(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
        {
            return info.rcWork;
        }
        // A headless/remote session can race monitor removal. Fall back to the
        // full virtual screen instead of throwing during a drag.
        return new RECT
        {
            Left = (int)SystemParameters.VirtualScreenLeft,
            Top = (int)SystemParameters.VirtualScreenTop,
            Right = (int)(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth),
            Bottom = (int)(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight),
        };
    }

    /// <summary>Clamps an absolute pixel rect to the monitor work area, then
    /// pulls any edge within <paramref name="snapPx"/> onto that edge.</summary>
    public static RECT Constrain(RECT rect, int snapPx, bool snap)
    {
        return ConstrainToWorkArea(rect, WorkAreaForRect(rect), snapPx, snap);
    }

    public static RECT ConstrainToWorkArea(RECT rect, RECT work, int snapPx, bool snap)
    {
        var width = Math.Max(1, rect.Width);
        var height = Math.Max(1, rect.Height);
        var left = rect.Left;
        var top = rect.Top;

        if (width <= work.Width)
        {
            left = Math.Clamp(left, work.Left, work.Right - width);
        }
        else
        {
            left = work.Left;
        }
        if (height <= work.Height)
        {
            top = Math.Clamp(top, work.Top, work.Bottom - height);
        }
        else
        {
            top = work.Top;
        }

        if (snap && width <= work.Width)
        {
            if (left - work.Left <= snapPx) left = work.Left;
            else if (work.Right - (left + width) <= snapPx) left = work.Right - width;
        }
        if (snap && height <= work.Height)
        {
            if (top - work.Top <= snapPx) top = work.Top;
            else if (work.Bottom - (top + height) <= snapPx) top = work.Bottom - height;
        }

        return new RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height,
        };
    }

    /// <summary>True when at least a small, grabbable portion of the rect lies
    /// inside a monitor work area.</summary>
    public static bool IsVisibleOnAnyMonitor(RECT rect, int minimumVisibleWidth = 80,
        int minimumVisibleHeight = 48)
    {
        var visible = false;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return true;
            var overlapWidth = Math.Min(rect.Right, info.rcWork.Right) -
                               Math.Max(rect.Left, info.rcWork.Left);
            var overlapHeight = Math.Min(rect.Bottom, info.rcWork.Bottom) -
                                Math.Max(rect.Top, info.rcWork.Top);
            if (overlapWidth >= Math.Min(minimumVisibleWidth, rect.Width) &&
                overlapHeight >= Math.Min(minimumVisibleHeight, rect.Height))
            {
                visible = true;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return visible;
    }

    /// <summary>Moves a rect that has no usable monitor overlap into the
    /// primary work area, keeping it fully visible.</summary>
    public static RECT RecoverToPrimary(RECT rect)
    {
        var work = PrimaryWorkArea();
        var width = Math.Min(Math.Max(1, rect.Width), work.Width);
        var height = Math.Min(Math.Max(1, rect.Height), work.Height);
        var left = Math.Clamp(rect.Left, work.Left, work.Right - width);
        var top = Math.Clamp(rect.Top, work.Top, work.Bottom - height);
        return new RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height,
        };
    }
}
