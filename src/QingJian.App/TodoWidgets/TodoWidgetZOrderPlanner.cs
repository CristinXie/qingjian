namespace QingJian.App.TodoWidgets;

public static class TodoWidgetZOrderPlanner
{
    public static readonly IntPtr HwndTop = IntPtr.Zero;
    public static readonly IntPtr HwndBottom = new(1);

    public static IntPtr? GetInsertAfterHandle(
        IntPtr widgetWindow,
        IntPtr desktopWindow,
        IReadOnlyList<IntPtr> zOrderTopToBottom)
    {
        return CreatePlan(widgetWindow, desktopWindow, zOrderTopToBottom).InsertAfterHandle;
    }

    public static TodoWidgetZOrderPlan CreatePlan(
        IntPtr widgetWindow,
        IntPtr? desktopWindow,
        IReadOnlyList<IntPtr> zOrderTopToBottom)
    {
        var desktopIndex = IndexOf(zOrderTopToBottom, desktopWindow);
        var widgetIndex = IndexOf(zOrderTopToBottom, widgetWindow);
        if (desktopIndex < 0 || widgetIndex < 0)
        {
            return TodoWidgetZOrderPlan.FallbackToBottom(HwndBottom);
        }

        if (widgetIndex == desktopIndex - 1)
        {
            return TodoWidgetZOrderPlan.NoMove();
        }

        for (var index = desktopIndex - 1; index >= 0; index--)
        {
            var candidate = zOrderTopToBottom[index];
            if (candidate != widgetWindow)
            {
                return TodoWidgetZOrderPlan.MoveAfter(candidate);
            }
        }

        return TodoWidgetZOrderPlan.MoveAfter(HwndTop);
    }

    private static int IndexOf(IReadOnlyList<IntPtr> handles, IntPtr? handle)
    {
        if (handle is null)
        {
            return -1;
        }

        for (var index = 0; index < handles.Count; index++)
        {
            if (handles[index] == handle.Value)
            {
                return index;
            }
        }

        return -1;
    }
}

public sealed record TodoWidgetZOrderPlan(
    bool ShouldMove,
    bool ShouldFallbackToBottom,
    IntPtr? InsertAfterHandle)
{
    public static TodoWidgetZOrderPlan NoMove()
    {
        return new TodoWidgetZOrderPlan(false, false, null);
    }

    public static TodoWidgetZOrderPlan MoveAfter(IntPtr insertAfterHandle)
    {
        return new TodoWidgetZOrderPlan(true, false, insertAfterHandle);
    }

    public static TodoWidgetZOrderPlan FallbackToBottom(IntPtr bottomHandle)
    {
        return new TodoWidgetZOrderPlan(true, true, bottomHandle);
    }
}
