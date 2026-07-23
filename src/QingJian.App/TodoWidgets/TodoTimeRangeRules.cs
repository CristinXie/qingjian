namespace QingJian.App.TodoWidgets;

public static class TodoTimeRangeRules
{
    public static bool IsValidRange(TimeOnly startTime, TimeOnly endTime)
    {
        return startTime != endTime;
    }

    public static bool IsOvernight(TimeOnly startTime, TimeOnly endTime)
    {
        return startTime > endTime;
    }
}
