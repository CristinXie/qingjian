namespace QingJian.App.Tray;

public interface ITrayIconService : IDisposable
{
    event EventHandler? OpenMainWindowRequested;

    event EventHandler? QuickNoteRequested;

    event EventHandler? ToggleTodoRequested;

    event EventHandler? ExitRequested;

    bool IsAvailable { get; }

    void Show();

    void SetTodoVisible(bool isVisible);
}
