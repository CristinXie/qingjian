using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetDraftParserTests
{
    [Fact]
    public void CreateDraft_IgnoresStaleTimeTextForNoTimeTodos()
    {
        var date = new DateOnly(2026, 7, 13);

        var draft = TodoWidgetDraftParser.CreateDraft(date, "Task", selectedTimeKindIndex: 0, "bad", "also bad");

        Assert.Equal(TodoTimeKind.None, draft.TimeKind);
        Assert.Null(draft.StartTime);
        Assert.Null(draft.EndTime);
    }

    [Fact]
    public void CreateDraft_IgnoresStaleEndTimeTextForSingleTimeTodos()
    {
        var date = new DateOnly(2026, 7, 13);

        var draft = TodoWidgetDraftParser.CreateDraft(date, "Task", selectedTimeKindIndex: 1, "09:30", "bad");

        Assert.Equal(TodoTimeKind.Single, draft.TimeKind);
        Assert.Equal(new TimeOnly(9, 30), draft.StartTime);
        Assert.Null(draft.EndTime);
    }

    [Fact]
    public void CreateDraft_ParsesTimeRange()
    {
        var date = new DateOnly(2026, 7, 13);

        var draft = TodoWidgetDraftParser.CreateDraft(date, "Task", selectedTimeKindIndex: 2, "09:30", "10:45");

        Assert.Equal(TodoTimeKind.Range, draft.TimeKind);
        Assert.Equal(new TimeOnly(9, 30), draft.StartTime);
        Assert.Equal(new TimeOnly(10, 45), draft.EndTime);
    }

    [Fact]
    public void CreateDraft_RejectsInvalidRequiredTime()
    {
        var date = new DateOnly(2026, 7, 13);

        Assert.Throws<FormatException>(() =>
            TodoWidgetDraftParser.CreateDraft(date, "Task", selectedTimeKindIndex: 1, "9点", string.Empty));
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesNoTimeTodoWhenBothTimesAreUnselected()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "Task", null, null);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(TodoTimeKind.None, result.Draft.TimeKind);
        Assert.Null(result.Draft.StartTime);
        Assert.Null(result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesSingleTimeTodoWhenOnlyStartIsSelected()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "Task", "09:30", null);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(TodoTimeKind.Single, result.Draft.TimeKind);
        Assert.Equal(new TimeOnly(9, 30), result.Draft.StartTime);
        Assert.Null(result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesRangeTodoWhenBothTimesAreSelected()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "Task", "09:30", "10:45");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(TodoTimeKind.Range, result.Draft.TimeKind);
        Assert.Equal(new TimeOnly(9, 30), result.Draft.StartTime);
        Assert.Equal(new TimeOnly(10, 45), result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesOvernightRangeTodoWhenStartIsLaterThanEnd()
    {
        var date = new DateOnly(2026, 7, 23);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "值班", "23:00", "01:00");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(date, result.Draft.Date);
        Assert.Equal(TodoTimeKind.Range, result.Draft.TimeKind);
        Assert.Equal(new TimeOnly(23, 0), result.Draft.StartTime);
        Assert.Equal(new TimeOnly(1, 0), result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_RejectsEmptyText()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, " ", "09:30", null);

        Assert.False(result.IsValid);
        Assert.Null(result.Draft);
        Assert.Equal("内容为空", result.TextError);
        Assert.Null(result.TimeError);
    }

    [Fact]
    public void ValidateQuickAddDraft_RejectsEndTimeWithoutStartTime()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "Task", null, "10:45");

        Assert.False(result.IsValid);
        Assert.Null(result.Draft);
        Assert.Null(result.TextError);
        Assert.Equal("时间选择不规范", result.TimeError);
    }

    [Fact]
    public void ValidateQuickAddDraft_RejectsRangeWhereTimesAreEqual()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "Task", "10:45", "10:45");

        Assert.False(result.IsValid);
        Assert.Null(result.Draft);
        Assert.Null(result.TextError);
        Assert.Equal("时间选择不规范", result.TimeError);
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesSingleTimeTodoFromSeparateHourAndMinuteInputs()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(
            date,
            "Task",
            startHourText: "9",
            startMinuteText: "05",
            endHourText: string.Empty,
            endMinuteText: string.Empty);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(TodoTimeKind.Single, result.Draft.TimeKind);
        Assert.Equal(new TimeOnly(9, 5), result.Draft.StartTime);
        Assert.Null(result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesRangeTodoFromSeparateHourAndMinuteInputs()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(
            date,
            "Task",
            startHourText: "09",
            startMinuteText: "00",
            endHourText: "10",
            endMinuteText: "30");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(TodoTimeKind.Range, result.Draft.TimeKind);
        Assert.Equal(new TimeOnly(9, 0), result.Draft.StartTime);
        Assert.Equal(new TimeOnly(10, 30), result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_CreatesOvernightRangeFromSeparateHourAndMinuteInputs()
    {
        var date = new DateOnly(2026, 7, 23);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(
            date,
            "值班",
            startHourText: "23",
            startMinuteText: "00",
            endHourText: "01",
            endMinuteText: "00");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Draft);
        Assert.Equal(date, result.Draft.Date);
        Assert.Equal(TodoTimeKind.Range, result.Draft.TimeKind);
        Assert.Equal(new TimeOnly(23, 0), result.Draft.StartTime);
        Assert.Equal(new TimeOnly(1, 0), result.Draft.EndTime);
    }

    [Fact]
    public void ValidateQuickAddDraft_RejectsIncompleteSeparateTimePoint()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(
            date,
            "Task",
            startHourText: "09",
            startMinuteText: string.Empty,
            endHourText: string.Empty,
            endMinuteText: string.Empty);

        Assert.False(result.IsValid);
        Assert.Null(result.Draft);
        Assert.Null(result.TextError);
        Assert.Equal("时间选择不规范", result.TimeError);
    }

    [Fact]
    public void ValidateQuickAddDraft_RejectsSeparateTimePointOutsideValidRanges()
    {
        var date = new DateOnly(2026, 7, 13);

        var result = TodoWidgetDraftParser.ValidateQuickAddDraft(
            date,
            "Task",
            startHourText: "24",
            startMinuteText: "00",
            endHourText: string.Empty,
            endMinuteText: string.Empty);

        Assert.False(result.IsValid);
        Assert.Null(result.Draft);
        Assert.Null(result.TextError);
        Assert.Equal("时间选择不规范", result.TimeError);
    }
}
