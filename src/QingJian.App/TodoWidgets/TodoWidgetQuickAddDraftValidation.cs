namespace QingJian.App.TodoWidgets;

public sealed record TodoWidgetQuickAddDraftValidation(
    TodoDraft? Draft,
    string? TextError,
    string? TimeError)
{
    public bool IsValid => Draft is not null;
}
