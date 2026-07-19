using System.Globalization;

namespace QingJian.App.TodoWidgets;

public static class TodoWidgetDraftParser
{
    private const string TimeFormat = "HH:mm";
    private const string EmptyContentMessage = "内容为空";
    private const string InvalidTimeSelectionMessage = "时间选择不规范";

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

    public static TodoWidgetQuickAddDraftValidation ValidateQuickAddDraft(
        DateOnly date,
        string text,
        string? selectedStartTime,
        string? selectedEndTime)
    {
        return ValidateQuickAddDraft(
            date,
            text,
            ParseOptionalTime(selectedStartTime),
            ParseOptionalTime(selectedEndTime),
            hasInvalidTimePoint: false);
    }

    public static TodoWidgetQuickAddDraftValidation ValidateQuickAddDraft(
        DateOnly date,
        string text,
        string? startHourText,
        string? startMinuteText,
        string? endHourText,
        string? endMinuteText)
    {
        var startTime = ParseOptionalTimePoint(startHourText, startMinuteText, out var hasInvalidStartTime);
        var endTime = ParseOptionalTimePoint(endHourText, endMinuteText, out var hasInvalidEndTime);

        return ValidateQuickAddDraft(
            date,
            text,
            startTime,
            endTime,
            hasInvalidStartTime || hasInvalidEndTime);
    }

    private static TodoWidgetQuickAddDraftValidation ValidateQuickAddDraft(
        DateOnly date,
        string text,
        TimeOnly? startTime,
        TimeOnly? endTime,
        bool hasInvalidTimePoint)
    {
        var textError = string.IsNullOrWhiteSpace(text) ? EmptyContentMessage : null;
        string? timeError = null;
        TodoTimeKind timeKind;

        if (hasInvalidTimePoint)
        {
            timeKind = TodoTimeKind.None;
            timeError = InvalidTimeSelectionMessage;
        }
        else if (startTime is null && endTime is null)
        {
            timeKind = TodoTimeKind.None;
        }
        else if (startTime is not null && endTime is null)
        {
            timeKind = TodoTimeKind.Single;
        }
        else if (startTime is not null && endTime is not null && startTime < endTime)
        {
            timeKind = TodoTimeKind.Range;
        }
        else
        {
            timeKind = TodoTimeKind.None;
            timeError = InvalidTimeSelectionMessage;
        }

        if (textError is not null || timeError is not null)
        {
            return new TodoWidgetQuickAddDraftValidation(null, textError, timeError);
        }

        return new TodoWidgetQuickAddDraftValidation(
            new TodoDraft(date, text, timeKind, startTime, timeKind == TodoTimeKind.Range ? endTime : null),
            null,
            null);
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

    private static TimeOnly? ParseOptionalTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeOnly.TryParseExact(
            value,
            TimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var time))
        {
            return time;
        }

        return null;
    }

    private static TimeOnly? ParseOptionalTimePoint(string? hourText, string? minuteText, out bool isInvalid)
    {
        var hasHour = !string.IsNullOrWhiteSpace(hourText);
        var hasMinute = !string.IsNullOrWhiteSpace(minuteText);
        if (!hasHour && !hasMinute)
        {
            isInvalid = false;
            return null;
        }

        if (!hasHour || !hasMinute ||
            !int.TryParse(hourText, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(minuteText, NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
            hour is < 0 or > 23 ||
            minute is < 0 or > 59)
        {
            isInvalid = true;
            return null;
        }

        isInvalid = false;
        return new TimeOnly(hour, minute);
    }
}
