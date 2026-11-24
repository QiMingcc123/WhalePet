using System.Globalization;
using System.Windows.Input;

namespace WhalePet;

/// <summary>Parses and formats user-editable hotkey strings such as "Ctrl+Alt+M" or "Alt+,".</summary>
public sealed class HotkeyGesture
{
    public uint Modifiers { get; init; }
    public uint VirtualKey { get; init; }
    public string Display { get; init; } = "";
    public string KeyText { get; init; } = "";

    public bool IsValid => VirtualKey != 0;

    public static uint ModifiersOf(ModifierKeys modifiers)
    {
        uint result = 0;
        if ((modifiers & ModifierKeys.Control) != 0) result |= HotkeyManager.ModControl;
        if ((modifiers & ModifierKeys.Alt) != 0) result |= HotkeyManager.ModAlt;
        if ((modifiers & ModifierKeys.Shift) != 0) result |= HotkeyManager.ModShift;
        if ((modifiers & ModifierKeys.Windows) != 0) result |= HotkeyManager.ModWin;
        return result;
    }

    public static HotkeyGesture? FromKey(Key key, ModifierKeys modifiers)
    {
        if (key == Key.System) return null;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
        {
            return null;
        }
        var (virtualKey, canonical) = FromWpfKey(key);
        if (virtualKey == 0 || canonical.Length == 0) return null;
        var mods = ModifiersOf(modifiers);
        var parts = new List<string>();
        if ((mods & HotkeyManager.ModControl) != 0) parts.Add("Ctrl");
        if ((mods & HotkeyManager.ModAlt) != 0) parts.Add("Alt");
        if ((mods & HotkeyManager.ModShift) != 0) parts.Add("Shift");
        if ((mods & HotkeyManager.ModWin) != 0) parts.Add("Win");
        parts.Add(canonical);
        return new HotkeyGesture
        {
            Modifiers = mods,
            VirtualKey = virtualKey,
            KeyText = canonical,
            Display = string.Join("+", parts),
        };
    }

    public bool Matches(Key key, ModifierKeys modifiers)
    {
        var candidate = FromKey(key, modifiers);
        return candidate is not null &&
               candidate.Modifiers == Modifiers &&
               candidate.VirtualKey == VirtualKey;
    }

    private static (uint VirtualKey, string Canonical) FromWpfKey(Key key) => key switch
    {
        Key.PageUp => (0x21, "PageUp"),
        Key.PageDown => (0x22, "PageDown"),
        Key.Home => (0x24, "Home"),
        Key.End => (0x23, "End"),
        Key.Space => (0x20, "Space"),
        Key.Enter => (0x0D, "Enter"),
        Key.Tab => (0x09, "Tab"),
        Key.OemComma => (0xBC, ","),
        Key.OemPeriod => (0xBE, "."),
        Key.OemQuestion => (0xBF, "/"),
        Key.OemSemicolon => (0xBA, ";"),
        Key.OemQuotes => (0xDE, "'"),
        Key.OemOpenBrackets => (0xDB, "["),
        Key.OemCloseBrackets => (0xDD, "]"),
        Key.OemMinus => (0xBD, "-"),
        Key.OemPlus => (0xBB, "="),
        >= Key.A and <= Key.Z => ((uint)('A' + ((int)key - (int)Key.A)),
            ((char)('A' + ((int)key - (int)Key.A))).ToString()),
        >= Key.D0 and <= Key.D9 => ((uint)('0' + ((int)key - (int)Key.D0)),
            ((char)('0' + ((int)key - (int)Key.D0))).ToString()),
        >= Key.F1 and <= Key.F12 => ((uint)(0x70 + ((int)key - (int)Key.F1)),
            "F" + ((int)key - (int)Key.F1 + 1)),
        _ => (0, ""),
    };

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = new HotkeyGesture();
        if (string.IsNullOrWhiteSpace(text)) return false;
        var raw = text.Trim();
        var parts = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        uint modifiers = 0;
        var keyPart = "";
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyManager.ModControl;
                    break;
                case "alt":
                    modifiers |= HotkeyManager.ModAlt;
                    break;
                case "shift":
                    modifiers |= HotkeyManager.ModShift;
                    break;
                case "win":
                case "windows":
                    modifiers |= HotkeyManager.ModWin;
                    break;
                default:
                    if (keyPart.Length > 0) return false;
                    keyPart = part;
                    break;
            }
        }
        if (keyPart.Length == 0) return false;

        var (vk, canonical) = ParseKey(keyPart);
        if (vk == 0) return false;
        var displayParts = new List<string>();
        if ((modifiers & HotkeyManager.ModControl) != 0) displayParts.Add("Ctrl");
        if ((modifiers & HotkeyManager.ModAlt) != 0) displayParts.Add("Alt");
        if ((modifiers & HotkeyManager.ModShift) != 0) displayParts.Add("Shift");
        if ((modifiers & HotkeyManager.ModWin) != 0) displayParts.Add("Win");
        displayParts.Add(canonical);
        gesture = new HotkeyGesture
        {
            Modifiers = modifiers,
            VirtualKey = vk,
            KeyText = canonical,
            Display = string.Join("+", displayParts),
        };
        return true;
    }

    public static string Normalize(string? text, string fallback) =>
        TryParse(text, out var gesture) ? gesture.Display : fallback;

    private static (uint VirtualKey, string Canonical) ParseKey(string text)
    {
        var key = text.Trim();
        if (key.Equals("PageUp", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("PgUp", StringComparison.OrdinalIgnoreCase)) return (0x21, "PageUp");
        if (key.Equals("PageDown", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("PgDn", StringComparison.OrdinalIgnoreCase)) return (0x22, "PageDown");
        if (key.Equals("Home", StringComparison.OrdinalIgnoreCase)) return (0x24, "Home");
        if (key.Equals("End", StringComparison.OrdinalIgnoreCase)) return (0x23, "End");
        if (key.Equals("Space", StringComparison.OrdinalIgnoreCase)) return (0x20, "Space");
        if (key.Equals("Enter", StringComparison.OrdinalIgnoreCase)) return (0x0D, "Enter");
        if (key.Equals("Tab", StringComparison.OrdinalIgnoreCase)) return (0x09, "Tab");
        if (key is "," or "，") return (0xBC, ",");
        if (key is "." or "。") return (0xBE, ".");
        if (key is "/" or "、") return (0xBF, "/");
        if (key is ";" or "；") return (0xBA, ";");
        if (key is "'" or "‘" or "’") return (0xDE, "'");
        if (key is "[" or "【") return (0xDB, "[");
        if (key is "]" or "】") return (0xDD, "]");
        if (key is "-" or "—") return (0xBD, "-");
        if (key is "=" or "＝") return (0xBB, "=");

        if (key.Length == 1)
        {
            var ch = char.ToUpperInvariant(key[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') return (ch, ch.ToString());
        }
        if (key.Length is >= 2 and <= 3 && key[0] is 'F' or 'f' &&
            int.TryParse(key[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) &&
            number is >= 1 and <= 12)
        {
            return ((uint)(0x70 + number - 1), "F" + number);
        }
        return (0, "");
    }
}
