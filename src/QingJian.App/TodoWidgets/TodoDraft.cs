namespace QingJian.App.TodoWidgets;

public sealed record TodoDraft(
    DateOnly Date,
    string Text,
    TodoTimeKind TimeKind,
    TimeOnly? StartTime,
    TimeOnly? EndTime);
