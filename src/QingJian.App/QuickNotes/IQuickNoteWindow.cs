namespace QingJian.App.QuickNotes;

public interface IQuickNoteWindow
{
    event EventHandler<QuickNoteDraft> SaveRequested;

    void ShowWindow();

    void CloseWindow();

    void ShowSaveError(string message);
}
