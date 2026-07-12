namespace QingJian.App.QuickNotes;

public interface IQuickNoteWindow
{
    event EventHandler<QuickNoteDraft> SaveRequested;

    void ShowWindow();

    bool TryBeginSave();

    void CompleteSave(bool succeeded);

    void CloseWindow();

    void ShowSaveError(string message);
}
