using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.ViewModels;

public sealed class RecycleBinViewModelTests
{
    [Fact]
    public async Task LoadAsync_LoadsRecentDeletedNotesAndTracksEmptyState()
    {
        var first = CreateDeletedNote("first");
        var second = CreateDeletedNote("second");
        var service = new TestNoteService(first, second);
        var viewModel = new RecycleBinViewModel(service);

        Assert.True(viewModel.IsEmpty);

        await viewModel.LoadAsync();

        Assert.Equal(new[] { "first", "second" }, viewModel.DeletedNotes.Select(note => note.Id));
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task RestoreAsync_RemovesCardOnlyAfterSuccessfulPersistence()
    {
        var note = CreateDeletedNote("note");
        var service = new TestNoteService(note);
        var viewModel = new RecycleBinViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.RestoreAsync(note);

        Assert.Empty(viewModel.DeletedNotes);
        Assert.Equal(new[] { "note" }, service.RestoredIds);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public async Task RestoreAsync_KeepsCardWhenPersistenceFails()
    {
        var note = CreateDeletedNote("note");
        var service = new TestNoteService(note)
        {
            RestoreException = new InvalidOperationException("boom")
        };
        var viewModel = new RecycleBinViewModel(service);
        await viewModel.LoadAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RestoreAsync(note));

        Assert.Contains(note, viewModel.DeletedNotes);
    }

    [Fact]
    public async Task PermanentlyDeleteAsync_RemovesCardOnlyAfterSuccessfulPersistence()
    {
        var note = CreateDeletedNote("note");
        var service = new TestNoteService(note);
        var viewModel = new RecycleBinViewModel(service);
        await viewModel.LoadAsync();

        await viewModel.PermanentlyDeleteAsync(note);

        Assert.Empty(viewModel.DeletedNotes);
        Assert.Equal(new[] { "note" }, service.PermanentlyDeletedIds);
    }

    private static Note CreateDeletedNote(string id)
    {
        var now = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
        return new Note
        {
            Id = id,
            Title = id,
            Content = "正文",
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddHours(-1),
            FolderName = "未分类",
            IsDeleted = true,
            DeletedAt = now
        };
    }

    private sealed class TestNoteService : INoteService
    {
        private readonly IReadOnlyList<Note> _deletedNotes;

        public TestNoteService(params Note[] deletedNotes)
        {
            _deletedNotes = deletedNotes;
        }

        public List<string> RestoredIds { get; } = new();

        public List<string> PermanentlyDeletedIds { get; } = new();

        public Exception? RestoreException { get; init; }

        public Task<IReadOnlyList<Note>> GetRecentlyDeletedNotesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_deletedNotes);

        public Task RestoreNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            if (RestoreException is not null)
            {
                throw RestoreException;
            }

            RestoredIds.Add(note.Id);
            return Task.CompletedTask;
        }

        public Task PermanentlyDeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
        {
            PermanentlyDeletedIds.Add(note.Id);
            return Task.CompletedTask;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Note> CreateNoteInFolderAsync(string folderName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetFavoriteAsync(Note note, bool isFavorite, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MoveNoteAsync(Note note, string folderName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetFavoritesAsync(IReadOnlyCollection<Note> notes, bool isFavorite, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MoveNotesAsync(IReadOnlyCollection<Note> notes, string folderName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteNotesAsync(IReadOnlyCollection<Note> notes, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
