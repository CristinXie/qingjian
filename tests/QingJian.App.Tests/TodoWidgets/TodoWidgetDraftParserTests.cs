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
}
