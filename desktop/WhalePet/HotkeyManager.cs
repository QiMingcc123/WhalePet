using System.Runtime.InteropServices;

namespace WhalePet;

public static class HotkeyManager
{
    public const int WmHotkey = 0x0312;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool Register(IntPtr hwnd, int id, uint modifiers, uint virtualKey) =>
        hwnd != IntPtr.Zero && virtualKey != 0 && RegisterHotKey(hwnd, id, modifiers, virtualKey);

    public static void Unregister(IntPtr hwnd, int id)
    {
        if (hwnd == IntPtr.Zero) return;
        try { UnregisterHotKey(hwnd, id); } catch { }
    }

    public static uint VirtualKey(string key)
    {
        var text = (key ?? "").Trim().ToUpperInvariant();
        if (text.Length == 1)
        {
            var ch = text[0];
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') return ch;
        }
        if (text.Length is >= 2 and <= 3 && text[0] == 'F' && int.TryParse(text[1..], out var number)
            && number is >= 1 and <= 12)
        {
            return (uint)(0x70 + number - 1);
        }
        return 0;
    }

    public static uint Modifiers(bool ctrl, bool alt, bool shift, bool win)
    {
        uint modifiers = 0;
        if (ctrl) modifiers |= ModControl;
        if (alt) modifiers |= ModAlt;
        if (shift) modifiers |= ModShift;
        if (win) modifiers |= ModWin;
        return modifiers;
    }

    public static string Describe(uint modifiers, string key)
    {
        var parts = new List<string>();
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & ModShift) != 0) parts.Add("Shift");
        if ((modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(key);
        return string.Join("+", parts);
    }
}
