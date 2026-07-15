using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace QingJian.App.TodoWidgets;

public sealed class WindowZOrderService
{
    private const string ProgmanClassName = "Progman";
    private static readonly IntPtr HwndBottom = new(1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;

    public bool MoveBehindOtherWindows(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var insertAfter = FindInsertAfterHandle(handle);
        return SetWindowPos(
            handle,
            insertAfter ?? HwndBottom,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
    }

    private static IntPtr? FindInsertAfterHandle(IntPtr widgetHandle)
    {
        var zOrder = new List<IntPtr>();
        IntPtr? desktopHandle = null;

        EnumWindows((handle, _) =>
        {
            zOrder.Add(handle);
            if (desktopHandle is null && GetWindowClassName(handle) == ProgmanClassName)
            {
                desktopHandle = handle;
            }

            return true;
        }, IntPtr.Zero);

        return desktopHandle is null
            ? null
            : TodoWidgetZOrderPlanner.GetInsertAfterHandle(widgetHandle, desktopHandle.Value, zOrder);
    }

    private static string GetWindowClassName(IntPtr handle)
    {
        var className = new StringBuilder(256);
        GetClassName(handle, className, className.Capacity);
        return className.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
