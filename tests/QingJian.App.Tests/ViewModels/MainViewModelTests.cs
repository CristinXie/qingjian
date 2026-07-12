using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task LoadAsync_LoadsNotesAndSelectsMostRecent()
    {
        var newest = CreateNote("new", "New");
        var older = CreateNote("old", "Old");
        var service = new InMemoryNoteService(newest, older);
        var viewModel = new MainViewModel(service);

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Notes.Count);
        Assert.Equal("new", viewModel.SelectedNote?.Id);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task NewNoteCommand_CreatesNoteAndSelectsIt()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.Notes.Count == 1 && ReferenceEquals(viewModel.SelectedNote, viewModel.Notes[0]));

        Assert.Single(viewModel.Notes);
        Assert.Same(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_CanExecute_TracksSelectedNoteState()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);

        Assert.False(viewModel.DeleteSelectedNoteCommand.CanExecute(null));

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.SelectedNote is not null);
        Assert.True(viewModel.DeleteSelectedNoteCommand.CanExecute(null));

        viewModel.DeleteSelectedNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.SelectedNote is null && viewModel.Notes.Count == 0);
        Assert.False(viewModel.DeleteSelectedNoteCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_DeletesSelectedNoteAndSelectsNextAvailableNote()
    {
        var first = CreateNote("first", "First");
        var second = CreateNote("second", "Second");
        var service = new InMemoryNoteService(first, second);
        var viewModel = new MainViewModel(service);
        await viewModel.LoadAsync();

        viewModel.DeleteSelectedNoteCommand.Execute(null);
        await WaitUntilAsync(() => viewModel.Notes.Count == 1 && viewModel.SelectedNote?.Id == "second" && service.DeletedIds.Contains("first"));

        Assert.Single(viewModel.Notes);
        Assert.Equal("second", viewModel.SelectedNote?.Id);
        Assert.DoesNotContain(viewModel.Notes, note => note.Id == "first");
        Assert.Contains("first", service.DeletedIds);
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_RaisesCanExecuteChanged_WhenSelectedNoteChanges()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var canExecuteChangedCount = 0;

        viewModel.DeleteSelectedNoteCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => canExecuteChangedCount > 0 && viewModel.SelectedNote is not null);

        Assert.True(canExecuteChangedCount > 0);
    }

    [Fact]
    public async Task IsEmpty_PropertyChanged_FiresWhenNotesTransitionBetweenEmptyAndNonEmpty()
    {
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var becameNonEmpty = false;
        var becameEmpty = false;

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainViewModel.IsEmpty))
            {
                return;
            }

            if (viewModel.Notes.Count == 1)
            {
                becameNonEmpty = true;
            }

            if (viewModel.Notes.Count == 0)
            {
                becameEmpty = true;
            }
        };

        viewModel.NewNoteCommand.Execute(null);
        await WaitUntilAsync(() => becameNonEmpty && viewModel.Notes.Count == 1);

        viewModel.DeleteSelectedNoteCommand.Execute(null);
        await WaitUntilAsync(() => becameEmpty && viewModel.Notes.Count == 0);

        Assert.True(becameNonEmpty);
        Assert.True(becameEmpty);
    }

    [Fact]
    public async Task SaveSelectedNoteNowAsync_SavesSelectedNote()
    {
        var note = CreateNote("note-1", "Original");
        var service = new InMemoryNoteService(note);
        var viewModel = new MainViewModel(service);
        await viewModel.LoadAsync();

        viewModel.SelectedNote!.Title = "Changed";
        await viewModel.SaveSelectedNoteNowAsync();

        Assert.Single(service.SavedIds);
        Assert.Equal("note-1", service.SavedIds[0]);
    }

    [Fact]
    public async Task EditingSelectedNote_AutoSavesAfterDelay()
    {
        var note = CreateNote("note-1", "Original");
        var service = new InMemoryNoteService(note);
        var viewModel = new MainViewModel(service, TimeSpan.FromMilliseconds(10));
        await viewModel.LoadAsync();

        viewModel.SelectedNote!.Content = "Changed";
        await WaitUntilAsync(() => service.SavedIds.Contains("note-1"));

        Assert.Contains("note-1", service.SavedIds);
    }

    [Fact]
    public void AddSavedNote_InsertsNoteAtTopAndSelectsWhenRequested()
    {
        var existing = CreateNote("existing", "Existing");
        var added = CreateNote("added", "Added");
        var service = new InMemoryNoteService(existing);
        var viewModel = new MainViewModel(service);
        viewModel.Notes.Add(existing);
        viewModel.SelectedNote = existing;

        viewModel.AddSavedNote(added, select: true);

        Assert.Equal("added", viewModel.Notes[0].Id);
        Assert.Same(added, viewModel.SelectedNote);
    }

    [Fact]
    public void AddSavedNote_DoesNotSelectWhenSelectIsFalse()
    {
        var existing = CreateNote("existing", "Existing");
        var added = CreateNote("added", "Added");
        var service = new InMemoryNoteService(existing);
        var viewModel = new MainViewModel(service);
        viewModel.Notes.Add(existing);
        viewModel.SelectedNote = existing;

        viewModel.AddSavedNote(added, select: false);

        Assert.Equal("added", viewModel.Notes[0].Id);
        Assert.Same(existing, viewModel.SelectedNote);
    }

    [Fact]
    public void AddSavedNote_RaisesIsEmptyChanged_WhenFirstNoteIsInserted()
    {
        var added = CreateNote("added", "Added");
        var service = new InMemoryNoteService();
        var viewModel = new MainViewModel(service);
        var raised = false;

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsEmpty))
            {
                raised = true;
            }
        };

        viewModel.AddSavedNote(added, select: true);

        Assert.True(raised);
        Assert.False(viewModel.IsEmpty);
    }

    private static Note CreateNote(string id, string title)
    {
        return new Note
        {
            Id = id,
            Title = title,
            Content = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private sealed class InMemoryNoteService : INoteService
    {
        private readonly List<Note> _notes;

        public InMemoryNoteService(params Note[] notes)
        {
            _notes = notes.ToList();
        }

        public List<string> DeletedIds { get; } = new();

        public List<string> SavedIds { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(_notes.ToList());
        }

        public Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default)
        {
            var note = CreateNote($"note-{_notes.Count + 1}", NoteService.DefaultTitle);
            _notes.Insert(0, note);
            return Task.FromResult(note);
        }

        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            SavedIds.Add(note.Id);
            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(note.Id);
            _notes.RemoveAll(item => item.Id == note.Id);
            return Task.CompletedTask;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellation.Token);
        }
    }
}
