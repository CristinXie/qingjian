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

        await viewModel.NewNoteAsync();

        Assert.Single(viewModel.Notes);
        Assert.Equal(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task DeleteSelectedNoteCommand_RemovesSelectedNoteAndSelectsNext()
    {
        var first = CreateNote("first", "First");
        var second = CreateNote("second", "Second");
        var service = new InMemoryNoteService(first, second);
        var viewModel = new MainViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.DeleteSelectedNoteAsync();

        Assert.Single(viewModel.Notes);
        Assert.Equal("second", viewModel.SelectedNote?.Id);
        Assert.Contains("first", service.DeletedIds);
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
            var note = CreateNote($"note-{_notes.Count + 1}", "未命名便签");
            _notes.Insert(0, note);
            return Task.FromResult(note);
        }

        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            DeletedIds.Add(note.Id);
            _notes.RemoveAll(item => item.Id == note.Id);
            return Task.CompletedTask;
        }
    }
}
