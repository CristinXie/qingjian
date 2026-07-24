namespace QingJian.App.Hotkeys;

public interface IGlobalHotkeyRegistrar
{
    event EventHandler? HotkeyPressed;

    bool TryRegister(HotkeyDefinition hotkey);

    void Unregister();
}
