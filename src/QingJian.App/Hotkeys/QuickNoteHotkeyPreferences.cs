namespace QingJian.App.Hotkeys;

public sealed record QuickNoteHotkeyPreferences(bool IsEnabled, uint Modifiers, uint VirtualKey)
{
    private static readonly HashSet<uint> ModifierVirtualKeys =
    [
        0x10,
        0x11,
        0x12,
        0x5B,
        0x5C
    ];

    public static QuickNoteHotkeyPreferences Default { get; } = new(
        IsEnabled: true,
        Modifiers: HotkeyDefinition.ModControl | HotkeyDefinition.ModAlt,
        VirtualKey: 0x4E);

    public QuickNoteHotkeyPreferences Normalize()
    {
        return IsValidGesture(Modifiers, VirtualKey)
            ? this
            : new QuickNoteHotkeyPreferences(IsEnabled, Default.Modifiers, Default.VirtualKey);
    }

    public static bool IsValidGesture(uint modifiers, uint virtualKey)
    {
        return modifiers != 0
            && (modifiers & ~HotkeyDefinition.AllowedModifiers) == 0
            && virtualKey is > 0 and < 0xFF
            && !ModifierVirtualKeys.Contains(virtualKey);
    }
}
