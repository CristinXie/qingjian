using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QingJian.App.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private int _registeredId;

    public event EventHandler? HotkeyPressed;

    public bool Register(Window window, HotkeyDefinition hotkey)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        if (!RegisterHotKey(_windowHandle, hotkey.Id, hotkey.Modifiers, hotkey.VirtualKey))
        {
            _source?.RemoveHook(WndProc);
            _source = null;
            _windowHandle = IntPtr.Zero;
            return false;
        }

        _registeredId = hotkey.Id;
        return true;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero && _registeredId != 0)
        {
            UnregisterHotKey(_windowHandle, _registeredId);
        }

        _source?.RemoveHook(WndProc);
        _source = null;
        _windowHandle = IntPtr.Zero;
        _registeredId = 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == _registeredId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
