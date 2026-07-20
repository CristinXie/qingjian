using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.Services;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class NoteServiceTests
{
    [Fact]
    public async Task CreateNoteAsync_CreatesDefaultNoteAndPersistsIt()
    {
        var repository = new InMemoryNoteRepository();
        var now = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
        var service = new NoteService(repository, () => now);

        var note = await service.CreateNoteAsync();

        Assert.Equal("未命名文件", note.Title);
        Assert.Equal(string.Empty, note.Content);
        Assert.Equal(now, note.CreatedAt);
        Assert.Equal(now, note.UpdatedAt);
        Assert.False(note.IsDeleted);
        Assert.Single(repository.Notes);
    }

    [Fact]
    public async Task SaveNoteAsync_NormalizesBlankTitleAndUpdatesTimestamp()
    {
        var repository = new InMemoryNoteRepository();
        var createdAt = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
        var savedAt = new DateTime(2026, 7, 8, 12, 5, 0, DateTimeKind.Utc);
        var service = new NoteService(repository, () => savedAt);
        var note = new Note
        {
            Id = "note-1",
            Title = "   ",
            Content = "Body",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsDeleted = false
        };

        await service.SaveNoteAsync(note);

        Assert.Equal("未命名文件", note.Title);
        Assert.Equal(savedAt, note.UpdatedAt);
        Assert.Single(repository.UpdatedNotes);
    }

    [Fact]
    public async Task SetFavoriteAsync_UpdatesFavoriteTimestampWithoutChangingUpdatedAt()
    {
        var repository = new InMemoryNoteRepository();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var favoritedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var note = CreateNote(updatedAt);
        var service = new NoteService(repository, () => favoritedAt);

        await service.SetFavoriteAsync(note, true);

        Assert.True(note.IsFavorite);
        Assert.Equal(favoritedAt, note.FavoritedAt);
        Assert.Equal(updatedAt, note.UpdatedAt);
        Assert.Single(repository.FavoriteUpdates);
    }

    [Fact]
    public async Task SetFavoriteAsync_RestoresOriginalValuesWhenPersistenceFails()
    {
        var repository = new InMemoryNoteRepository
        {
            FavoriteUpdateException = new InvalidOperationException("boom")
        };
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var originalFavoriteTime = new DateTime(2026, 7, 19, 9, 0, 0, DateTimeKind.Utc);
        var note = CreateNote(updatedAt);
        note.IsFavorite = true;
        note.FavoritedAt = originalFavoriteTime;
        var service = new NoteService(repository, () => updatedAt.AddHours(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetFavoriteAsync(note, false));

        Assert.True(note.IsFavorite);
        Assert.Equal(originalFavoriteTime, note.FavoritedAt);
        Assert.Equal(updatedAt, note.UpdatedAt);
    }

    private static Note CreateNote(DateTime updatedAt)
    {
        return new Note
        {
            Id = "note-1",
            Title = "Title",
            Content = "Body",
            CreatedAt = updatedAt.AddMinutes(-1),
            UpdatedAt = updatedAt,
            FolderName = "未分类",
            IsDeleted = false
        };
    }

    private sealed class InMemoryNoteRepository : INoteRepository
    {
        public List<Note> Notes { get; } = new();

        public List<Note> UpdatedNotes { get; } = new();

        public List<Note> FavoriteUpdates { get; } = new();

        public Exception? FavoriteUpdateException { get; init; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(Notes.Where(note => !note.IsDeleted).ToList());
        }

        public Task AddAsync(Note note, CancellationToken cancellationToken = default)
        {
            Notes.Add(note);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
        {
            UpdatedNotes.Add(note);
            return Task.CompletedTask;
        }

        public Task UpdateFavoriteAsync(Note note, CancellationToken cancellationToken = default)
        {
            if (FavoriteUpdateException is not null)
            {
                throw FavoriteUpdateException;
            }

            FavoriteUpdates.Add(note);
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
        {
            var note = Notes.Single(item => item.Id == noteId);
            note.IsDeleted = true;
            note.UpdatedAt = deletedAt;
            return Task.CompletedTask;
        }
    }
}
