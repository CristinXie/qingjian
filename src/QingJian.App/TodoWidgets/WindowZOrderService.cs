using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace QingJian.App.TodoWidgets;

public sealed class WindowZOrderService
{
    private const string ProgmanClassName = "Progman";
    private const int GwlpHwndParent = -8;
    private const uint GwOwner = 4;
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

        return MoveBehindOtherWindows(handle);
    }

    public bool MoveBehindOtherWindows(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var plan = CreateZOrderPlan(handle);
        if (!plan.ShouldMove || plan.InsertAfterHandle is null)
        {
            return true;
        }

        return SetWindowPos(
            handle,
            plan.InsertAfterHandle.Value,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
    }

    public bool AttachToDesktopOwner(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        return AttachToDesktopOwner(handle);
    }

    public bool AttachToDesktopOwner(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var desktopHandle = FindDesktopHandle();
        if (desktopHandle is null)
        {
            return false;
        }

        SetWindowOwner(handle, desktopHandle.Value);
        return GetWindow(handle, GwOwner) == desktopHandle.Value;
    }

    private static TodoWidgetZOrderPlan CreateZOrderPlan(IntPtr widgetHandle)
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

        return TodoWidgetZOrderPlanner.CreatePlan(widgetHandle, desktopHandle, zOrder);
    }

    private static IntPtr? FindDesktopHandle()
    {
        IntPtr? desktopHandle = null;

        EnumWindows((handle, _) =>
        {
            if (GetWindowClassName(handle) == ProgmanClassName)
            {
                desktopHandle = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return desktopHandle;
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

    private static IntPtr SetWindowOwner(IntPtr handle, IntPtr ownerHandle)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, GwlpHwndParent, ownerHandle)
            : new IntPtr(SetWindowLong32(handle, GwlpHwndParent, ownerHandle.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

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
