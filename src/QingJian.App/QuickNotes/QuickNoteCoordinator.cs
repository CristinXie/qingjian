using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.QuickNotes;

public sealed class QuickNoteCoordinator
{
    private readonly INoteService _noteService;
    private readonly MainViewModel _mainViewModel;
    private readonly IQuickNoteWindowFactory _windowFactory;
    private readonly Func<bool> _shouldSyncToMainWindow;

    public QuickNoteCoordinator(
        INoteService noteService,
        MainViewModel mainViewModel,
        IQuickNoteWindowFactory windowFactory,
        Func<bool> shouldSyncToMainWindow)
    {
        _noteService = noteService;
        _mainViewModel = mainViewModel;
        _windowFactory = windowFactory;
        _shouldSyncToMainWindow = shouldSyncToMainWindow;
    }

    public void OpenQuickNote()
    {
        var window = _windowFactory.Create();
        window.SaveRequested += OnSaveRequested;
        window.ShowWindow();
    }

    public async Task<QuickNoteSaveResult> SaveDraftAsync(
        QuickNoteDraft draft,
        bool syncToMainWindow,
        CancellationToken cancellationToken = default)
    {
        if (!QuickNoteTitleGenerator.HasBody(draft.Body))
        {
            return QuickNoteSaveResult.Failure("请输入便签内容。");
        }

        var body = draft.Body;
        var note = await _noteService.CreateNoteAsync(cancellationToken);

        try
        {
            note.Title = QuickNoteTitleGenerator.CreateTitle(draft.Title, body);
            note.Content = body;
            await _noteService.SaveNoteAsync(note, cancellationToken);
        }
        catch
        {
            await _noteService.DeleteNoteAsync(note, CancellationToken.None);
            throw;
        }

        if (syncToMainWindow)
        {
            _mainViewModel.AddSavedNote(note, select: true);
        }

        return QuickNoteSaveResult.Success(note);
    }

    private async void OnSaveRequested(object? sender, QuickNoteDraft draft)
    {
        if (sender is not IQuickNoteWindow window)
        {
            return;
        }

        if (!window.TryBeginSave())
        {
            return;
        }

        try
        {
            var result = await SaveDraftAsync(draft, _shouldSyncToMainWindow());
            if (result.Succeeded)
            {
                window.CompleteSave(succeeded: true);
                window.CloseWindow();
                return;
            }

            window.CompleteSave(succeeded: false);
            window.ShowSaveError(result.ErrorMessage ?? "保存失败。");
        }
        catch (Exception ex)
        {
            window.CompleteSave(succeeded: false);
            window.ShowSaveError($"保存失败：{ex.Message}");
        }
    }
}
