using QingJian.App.Models;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteCoordinatorTests
{
    [Fact]
    public async Task SaveDraftAsync_RejectsEmptyBody()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "   "), syncToMainWindow: true);

        Assert.False(result.Succeeded);
        Assert.Null(result.Note);
        Assert.Equal("请输入便签内容。", result.ErrorMessage);
        Assert.Empty(service.SavedNotes);
    }

    [Fact]
    public async Task SaveDraftAsync_CreatesAndSavesNoteWithGeneratedTitle()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft(null, "First line\nSecond line"), syncToMainWindow: false);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Note);
        Assert.Equal("First line", result.Note.Title);
        Assert.Equal("First line\nSecond line", result.Note.Content);
        Assert.Single(service.SavedNotes);
    }

    [Fact]
    public async Task SaveDraftAsync_PreservesBodyWhitespace()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft(null, "  Body  \n"), syncToMainWindow: false);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Note);
        Assert.Equal("Body", result.Note.Title);
        Assert.Equal("  Body  \n", result.Note.Content);
    }

    [Fact]
    public async Task SaveDraftAsync_SyncsToMainViewModel_WhenRequested()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "Body"), syncToMainWindow: true);

        Assert.True(result.Succeeded);
        Assert.Single(viewModel.Notes);
        Assert.Same(result.Note, viewModel.SelectedNote);
    }

    [Fact]
    public async Task SaveDraftAsync_DoesNotSyncToMainViewModel_WhenNotRequested()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        var result = await coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "Body"), syncToMainWindow: false);

        Assert.True(result.Succeeded);
        Assert.Empty(viewModel.Notes);
    }

    [Fact]
    public async Task SaveDraftAsync_DeletesCreatedNote_WhenSaveFails()
    {
        var service = new InMemoryNoteService
        {
            SaveException = new InvalidOperationException("boom")
        };
        var viewModel = new MainViewModel(service);
        var coordinator = new QuickNoteCoordinator(service, viewModel, new TestWindowFactory(), () => false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.SaveDraftAsync(new QuickNoteDraft("Title", "Body"), syncToMainWindow: false));

        Assert.Equal(new[] { "note-1" }, service.DeletedIds);
        Assert.Empty(service.ActiveNotes);
        Assert.Empty(service.SavedNotes);
    }

    [Fact]
    public void OpenQuickNote_CreatesNewWindowEachTime()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var factory = new TestWindowFactory();
        var coordinator = new QuickNoteCoordinator(service, viewModel, factory, () => false);

        coordinator.OpenQuickNote();
        coordinator.OpenQuickNote();

        Assert.Equal(2, factory.Windows.Count);
        Assert.All(factory.Windows, window => Assert.True(window.WasShown));
    }

    [Fact]
    public async Task OpenQuickNote_SaveRequested_UsesSyncSettingAndClosesWindowOnSuccess()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var factory = new TestWindowFactory();
        var coordinator = new QuickNoteCoordinator(service, viewModel, factory, () => true);

        coordinator.OpenQuickNote();
        var window = Assert.Single(factory.Windows);

        window.RequestSave(new QuickNoteDraft("Title", "Body"));
        await window.WaitForCloseAsync();

        Assert.Single(viewModel.Notes);
        Assert.Single(service.SavedNotes);
        Assert.True(window.WasClosed);
        Assert.Null(window.LastSaveError);
    }

    [Fact]
    public async Task OpenQuickNote_SaveRequested_ShowsErrorAndKeepsWindowOpenOnFailure()
    {
        var service = new InMemoryNoteService
        {
            SaveException = new InvalidOperationException("boom")
        };
        var viewModel = new MainViewModel(service);
        var factory = new TestWindowFactory();
        var coordinator = new QuickNoteCoordinator(service, viewModel, factory, () => false);

        coordinator.OpenQuickNote();
        var window = Assert.Single(factory.Windows);

        window.RequestSave(new QuickNoteDraft("Title", "Body"));
        var error = await window.WaitForErrorAsync();

        Assert.Equal("保存失败：boom", error);
        Assert.False(window.WasClosed);
        Assert.Empty(viewModel.Notes);
        Assert.Equal(new[] { "note-1" }, service.DeletedIds);
    }

    private sealed class InMemoryNoteService : INoteService
    {
        private readonly List<Note> _notes = new();

        public IReadOnlyList<Note> ActiveNotes => _notes;

        public List<Note> SavedNotes { get; } = new();

        public List<string> DeletedIds { get; } = new();

        public Exception? SaveException { get; init; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(_notes.ToList());
        }

        public Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default)
        {
            var note = new Note
            {
                Id = $"note-{_notes.Count + 1}",
                Title = NoteService.DefaultTitle,
                Content = string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            _notes.Add(note);
            return Task.FromResult(note);
        }

        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedNotes.Add(note);
            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(note.Id);
            _notes.Remove(note);
            note.IsDeleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestWindowFactory : IQuickNoteWindowFactory
    {
        public List<TestQuickNoteWindow> Windows { get; } = new();

        public IQuickNoteWindow Create()
        {
            var window = new TestQuickNoteWindow();
            Windows.Add(window);
            return window;
        }
    }

    private sealed class TestQuickNoteWindow : IQuickNoteWindow
    {
        public event EventHandler<QuickNoteDraft>? SaveRequested;

        public bool WasShown { get; private set; }

        public bool WasClosed { get; private set; }

        public string? LastSaveError { get; private set; }

        private TaskCompletionSource<bool> CloseSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource<string> ErrorSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ShowWindow()
        {
            WasShown = true;
        }

        public void CloseWindow()
        {
            WasClosed = true;
            CloseSignal.TrySetResult(true);
        }

        public void ShowSaveError(string message)
        {
            LastSaveError = message;
            ErrorSignal.TrySetResult(message);
        }

        public void RequestSave(QuickNoteDraft draft)
        {
            SaveRequested?.Invoke(this, draft);
        }

        public Task WaitForCloseAsync() => CloseSignal.Task;

        public Task<string> WaitForErrorAsync() => ErrorSignal.Task;
    }
}
