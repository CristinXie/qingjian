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

    private sealed class InMemoryNoteService : INoteService
    {
        private readonly List<Note> _notes = new();

        public List<Note> SavedNotes { get; } = new();

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
            SavedNotes.Add(note);
            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

        public void ShowWindow()
        {
            WasShown = true;
        }

        public void CloseWindow()
        {
        }

        public void ShowSaveError(string message)
        {
        }

        public void RequestSave(QuickNoteDraft draft)
        {
            SaveRequested?.Invoke(this, draft);
        }
    }
}
