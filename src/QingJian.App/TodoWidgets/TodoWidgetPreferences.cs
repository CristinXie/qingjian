namespace QingJian.App.TodoWidgets;

public sealed record TodoWidgetPreferences(
    bool IsVisible,
    TodoWidgetMode Mode,
    double Left,
    double Top,
    double Opacity,
    bool IsLocked,
    int CalendarYear,
    int CalendarMonth)
{
    public static TodoWidgetPreferences Default { get; } = new(
        IsVisible: true,
        Mode: TodoWidgetMode.EightDay,
        Left: 80,
        Top: 80,
        Opacity: 0.75,
        IsLocked: false,
        CalendarYear: DateTime.Today.Year,
        CalendarMonth: DateTime.Today.Month);

    public TodoWidgetPreferences Normalize()
    {
        var mode = Enum.IsDefined(typeof(TodoWidgetMode), Mode)
            ? Mode
            : TodoWidgetMode.EightDay;
        var opacity = double.IsFinite(Opacity) && Opacity is >= 0.35 and <= 0.95
            ? Opacity
            : Default.Opacity;
        var left = double.IsFinite(Left) && Left > -10000 ? Left : Default.Left;
        var top = double.IsFinite(Top) && Top > -10000 ? Top : Default.Top;
        var calendarYear = CalendarYear is >= 1900 and <= 9999 ? CalendarYear : DateTime.Today.Year;
        var calendarMonth = CalendarMonth is >= 1 and <= 12 ? CalendarMonth : DateTime.Today.Month;

        return this with
        {
            Mode = mode,
            Left = left,
            Top = top,
            Opacity = opacity,
            CalendarYear = calendarYear,
            CalendarMonth = calendarMonth
        };
    }
}
