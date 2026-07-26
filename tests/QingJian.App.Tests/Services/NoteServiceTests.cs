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

    [Fact]
    public async Task CreateAndMoveNoteAsync_PersistsFolderWithoutChangingUpdatedAt()
    {
        var repository = new InMemoryNoteRepository();
        var folderService = new FakeFolderService("项目", "工作");
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var service = new NoteService(repository, folderService, () => updatedAt);

        var note = await service.CreateNoteInFolderAsync("项目");
        await service.MoveNoteAsync(note, "工作");

        Assert.Equal("工作", note.FolderName);
        Assert.Equal(updatedAt, note.UpdatedAt);
        Assert.Single(repository.FolderUpdates);
    }

    [Fact]
    public async Task MoveNoteAsync_RestoresFolderWhenPersistenceFails()
    {
        var repository = new InMemoryNoteRepository
        {
            FolderUpdateException = new InvalidOperationException("boom")
        };
        var note = CreateNote(new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc));
        var folderService = new FakeFolderService("项目", "工作");
        var service = new NoteService(repository, folderService, () => note.UpdatedAt);
        repository.Notes.Add(note);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MoveNoteAsync(note, "工作"));

        Assert.Equal("未分类", note.FolderName);
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

        public List<Note> FolderUpdates { get; } = new();

        public Exception? FavoriteUpdateException { get; init; }

        public Exception? FolderUpdateException { get; init; }

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

        public Task UpdateFolderAsync(Note note, CancellationToken cancellationToken = default)
        {
            if (FolderUpdateException is not null)
            {
                throw FolderUpdateException;
            }

            FolderUpdates.Add(note);
            return Task.CompletedTask;
        }

        public Task SetFavoritesAsync(
            IReadOnlyCollection<string> noteIds,
            bool isFavorite,
            DateTime? favoritedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MoveToFolderAsync(
            IReadOnlyCollection<string> noteIds,
            string folderName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
        {
            var note = Notes.Single(item => item.Id == noteId);
            note.IsDeleted = true;
            note.UpdatedAt = deletedAt;
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(
            IReadOnlyCollection<string> noteIds,
            DateTime deletedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFolderService : IFolderService
    {
        private readonly HashSet<string> _folderNames = new(StringComparer.OrdinalIgnoreCase);

        public FakeFolderService(params string[] folderNames)
        {
            _folderNames.UnionWith(folderNames);
            _folderNames.Add(FolderNamePolicy.UncategorizedName);
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FolderSummary>>(Array.Empty<FolderSummary>());

        public Task<FolderSummary> CreateAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FolderSummary> RenameAsync(FolderSummary folder, string newName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> IsValidMoveTargetAsync(string folderName, CancellationToken cancellationToken = default)
            => Task.FromResult(_folderNames.Contains(folderName));
    }
}
