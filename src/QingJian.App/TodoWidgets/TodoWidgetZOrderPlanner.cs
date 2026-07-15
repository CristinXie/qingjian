namespace QingJian.App.TodoWidgets;

public static class TodoWidgetZOrderPlanner
{
    public static readonly IntPtr HwndTop = IntPtr.Zero;

    public static IntPtr? GetInsertAfterHandle(
        IntPtr widgetWindow,
        IntPtr desktopWindow,
        IReadOnlyList<IntPtr> zOrderTopToBottom)
    {
        var desktopIndex = IndexOf(zOrderTopToBottom, desktopWindow);
        var widgetIndex = IndexOf(zOrderTopToBottom, widgetWindow);
        if (desktopIndex < 0 || widgetIndex < 0)
        {
            return null;
        }

        if (widgetIndex == desktopIndex - 1)
        {
            return null;
        }

        for (var index = desktopIndex - 1; index >= 0; index--)
        {
            var candidate = zOrderTopToBottom[index];
            if (candidate != widgetWindow)
            {
                return candidate;
            }
        }

        return HwndTop;
    }

    private static int IndexOf(IReadOnlyList<IntPtr> handles, IntPtr handle)
    {
        for (var index = 0; index < handles.Count; index++)
        {
            if (handles[index] == handle)
            {
                return index;
            }
        }

        return -1;
    }
}
