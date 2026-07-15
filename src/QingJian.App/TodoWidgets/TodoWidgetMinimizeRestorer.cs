using System.Windows;

namespace QingJian.App.TodoWidgets;

public static class TodoWidgetMinimizeRestorer
{
    public static bool ShouldRestore(WindowState windowState, bool isHidingFromButton, bool isVisible)
    {
        return windowState == WindowState.Minimized && !isHidingFromButton && isVisible;
    }
}
