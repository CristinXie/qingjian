using System.Windows.Input;

namespace QingJian.App.Hotkeys;

public static class HotkeyGestureFormatter
{
    public static string Format(uint modifiers, uint virtualKey)
    {
        if (!QuickNoteHotkeyPreferences.IsValidGesture(modifiers, virtualKey))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        AddModifier(parts, modifiers, HotkeyDefinition.ModControl, "Ctrl");
        AddModifier(parts, modifiers, HotkeyDefinition.ModAlt, "Alt");
        AddModifier(parts, modifiers, HotkeyDefinition.ModShift, "Shift");
        AddModifier(parts, modifiers, HotkeyDefinition.ModWin, "Win");
        parts.Add(FormatKey(virtualKey));
        return string.Join(" + ", parts);
    }

    private static void AddModifier(List<string> parts, uint modifiers, uint modifier, string text)
    {
        if ((modifiers & modifier) != 0)
        {
            parts.Add(text);
        }
    }

    private static string FormatKey(uint virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x6F}";
        }

        return KeyInterop.KeyFromVirtualKey((int)virtualKey).ToString();
    }
}
