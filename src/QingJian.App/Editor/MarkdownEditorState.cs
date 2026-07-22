namespace QingJian.App.Editor;

public sealed class MarkdownEditorState
{
    private bool _isLoadingFromSelection;

    public string? CurrentNoteId { get; private set; }

    public string CurrentMarkdown { get; private set; } = string.Empty;

    public string BeginLoad(string? noteId, string? markdown)
    {
        _isLoadingFromSelection = true;
        CurrentNoteId = noteId;
        CurrentMarkdown = markdown ?? string.Empty;
        return CurrentMarkdown;
    }

    public void EndLoad()
    {
        _isLoadingFromSelection = false;
    }

    public bool ShouldReloadSelection(string? noteId)
    {
        return !string.Equals(CurrentNoteId, noteId, StringComparison.Ordinal);
    }

    public bool TryApplyEditorMarkdown(string? noteId, string markdown, out string normalizedMarkdown)
    {
        normalizedMarkdown = markdown ?? string.Empty;

        if (_isLoadingFromSelection)
        {
            normalizedMarkdown = CurrentMarkdown;
            return false;
        }

        if (noteId != CurrentNoteId)
        {
            normalizedMarkdown = CurrentMarkdown;
            return false;
        }

        if (CurrentMarkdown == normalizedMarkdown)
        {
            return false;
        }

        CurrentMarkdown = normalizedMarkdown;
        return true;
    }
}
