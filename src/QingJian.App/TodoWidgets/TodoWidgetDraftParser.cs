using System.Globalization;

namespace QingJian.App.TodoWidgets;

public static class TodoWidgetDraftParser
{
    private const string TimeFormat = "HH:mm";

    public static TodoDraft CreateDraft(
        DateOnly date,
        string text,
        int selectedTimeKindIndex,
        string startTimeText,
        string endTimeText)
    {
        var timeKind = selectedTimeKindIndex switch
        {
            1 => TodoTimeKind.Single,
            2 => TodoTimeKind.Range,
            _ => TodoTimeKind.None
        };

        return timeKind switch
        {
            TodoTimeKind.None => new TodoDraft(date, text, timeKind, null, null),
            TodoTimeKind.Single => new TodoDraft(
                date,
                text,
                timeKind,
                ParseRequiredTime(startTimeText),
                null),
            TodoTimeKind.Range => new TodoDraft(
                date,
                text,
                timeKind,
                ParseRequiredTime(startTimeText),
                ParseRequiredTime(endTimeText)),
            _ => throw new ArgumentOutOfRangeException(nameof(selectedTimeKindIndex), "Unsupported todo time kind.")
        };
    }

    private static TimeOnly ParseRequiredTime(string value)
    {
        if (TimeOnly.TryParseExact(
            value,
            TimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var time))
        {
            return time;
        }

        throw new FormatException("时间格式应为 HH:mm。");
    }
}
