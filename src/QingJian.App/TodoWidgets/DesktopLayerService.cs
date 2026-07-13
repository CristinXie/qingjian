using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QingJian.App.TodoWidgets;

public sealed class DesktopLayerService
{
    private const int WmSpawnWorker = 0x052C;

    public bool TryAttachToDesktop(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return false;
        }

        SendMessageTimeout(
            progman,
            WmSpawnWorker,
            IntPtr.Zero,
            IntPtr.Zero,
            SendMessageTimeoutFlags.SMTO_NORMAL,
            1000,
            out _);

        var workerW = FindDesktopWorkerW();
        if (workerW == IntPtr.Zero)
        {
            return false;
        }

        return SetParent(handle, workerW) != IntPtr.Zero;
    }

    private static IntPtr FindDesktopWorkerW()
    {
        var result = IntPtr.Zero;

        EnumWindows((topHandle, _) =>
        {
            var shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                return true;
            }

            result = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
            return result == IntPtr.Zero;
        }, IntPtr.Zero);

        return result;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        int timeout,
        out IntPtr result);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000
    }
}
