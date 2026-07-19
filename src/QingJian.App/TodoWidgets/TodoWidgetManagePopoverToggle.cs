namespace QingJian.App.TodoWidgets;

public static class TodoWidgetManagePopoverToggle
{
    public static bool ShouldClose(bool isVisible, DateOnly? pinnedDate, DateOnly clickedDate)
    {
        return isVisible && pinnedDate == clickedDate;
    }
}
