namespace QingJian.App.Hotkeys;

public sealed record HotkeyDefinition(int Id, uint Modifiers, uint VirtualKey, string DisplayText)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;

    public static HotkeyDefinition QuickNoteHotkey { get; } = new(
        Id: 0x514A01,
        Modifiers: ModControl | ModAlt,
        VirtualKey: 0x4E,
        DisplayText: "Ctrl + Alt + N");
}
