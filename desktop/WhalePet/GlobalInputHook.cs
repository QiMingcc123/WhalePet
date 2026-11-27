using System.Runtime.InteropServices;

namespace WhalePet;

/// <summary>Observes system-wide keyboard and mouse activity for the pet's reaction badges.</summary>
public sealed class GlobalInputHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONUP = 0x0208;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    private readonly HookProc _callback;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _disposed;

    public event Action? KeyboardPressed;
    public event Action<int>? KeyPressed;
    public event Action<int, int>? PointerMoved;
    public event Action<int, int>? PointerPressed;
    public event Action<int, int, int>? PointerButtonPressed;
    public event Action<int, int, int>? PointerButtonReleased;

    public GlobalInputHook()
    {
        _callback = Hook;
        var module = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _callback, module, 0);
        GC.KeepAlive(_callback);
    }

    private IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    var virtualKey = wParam.ToInt32();
                    KeyboardPressed?.Invoke();
                    KeyPressed?.Invoke(virtualKey);
                }
                else if (message == WM_MOUSEMOVE)
                {
                    if (GetCursorPos(out var point)) PointerMoved?.Invoke(point.X, point.Y);
                }
                else if (message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN || message == WM_MBUTTONDOWN)
                {
                    if (GetCursorPos(out var point))
                    {
                        PointerPressed?.Invoke(point.X, point.Y);
                        PointerButtonPressed?.Invoke(point.X, point.Y,
                            message == WM_LBUTTONDOWN ? 1 : message == WM_RBUTTONDOWN ? 2 : 3);
                    }
                }
                else if (message == WM_LBUTTONUP || message == WM_RBUTTONUP || message == WM_MBUTTONUP)
                {
                    if (GetCursorPos(out var point))
                    {
                        PointerButtonReleased?.Invoke(point.X, point.Y,
                            message == WM_LBUTTONUP ? 1 : message == WM_RBUTTONUP ? 2 : 3);
                    }
                }
            }
        }
        catch
        {
            // A consumer failure must never break the system hook chain.
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }
}
