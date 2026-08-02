namespace QingJian.App.TodoWidgets;

public static class TodoWidgetVisibilityAction
{
    public static string GetLabel(bool isVisible)
    {
        return isVisible ? "隐藏桌面待办" : "显示桌面待办";
    }
}
