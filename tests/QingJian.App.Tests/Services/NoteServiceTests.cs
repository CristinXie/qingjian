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

    [Fact]
    public async Task SetFavoritesAsync_UsesOneTimestampAndPreservesExistingFavorites()
    {
        var repository = new InMemoryNoteRepository();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var now = updatedAt.AddHours(2);
        var normal = CreateNote(updatedAt, "normal");
        var existing = CreateNote(updatedAt.AddMinutes(1), "existing");
        var existingFavoriteTime = updatedAt.AddHours(1);
        existing.IsFavorite = true;
        existing.FavoritedAt = existingFavoriteTime;
        var service = new NoteService(repository, () => now);

        await service.SetFavoritesAsync(new[] { normal, normal, existing }, true);

        Assert.True(normal.IsFavorite);
        Assert.Equal(now, normal.FavoritedAt);
        Assert.Equal(updatedAt, normal.UpdatedAt);
        Assert.True(existing.IsFavorite);
        Assert.Equal(existingFavoriteTime, existing.FavoritedAt);
        var update = Assert.Single(repository.BatchFavoriteUpdates);
        Assert.Equal(new[] { "normal" }, update.NoteIds);
        Assert.True(update.IsFavorite);
        Assert.Equal(now, update.FavoritedAt);
    }

    [Fact]
    public async Task SetFavoritesAsync_DoesNotChangeMemoryWhenPersistenceFails()
    {
        var repository = new InMemoryNoteRepository
        {
            BatchFavoriteUpdateException = new InvalidOperationException("boom")
        };
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var first = CreateNote(updatedAt, "first");
        var second = CreateNote(updatedAt.AddMinutes(1), "second");
        var service = new NoteService(repository, () => updatedAt.AddHours(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetFavoritesAsync(new[] { first, second }, true));

        Assert.All(new[] { first, second }, note =>
        {
            Assert.False(note.IsFavorite);
            Assert.Null(note.FavoritedAt);
        });
    }

    [Fact]
    public async Task MoveNotesAsync_ValidatesOnceAndUpdatesOnlyChangedNotesAfterPersistence()
    {
        var repository = new InMemoryNoteRepository();
        var folderService = new FakeFolderService("工作");
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var first = CreateNote(updatedAt, "first");
        var alreadyThere = CreateNote(updatedAt.AddMinutes(1), "existing");
        alreadyThere.FolderName = "工作";
        var service = new NoteService(repository, folderService, () => updatedAt.AddHours(1));

        await service.MoveNotesAsync(new[] { first, first, alreadyThere }, " 工作 ");

        Assert.Equal("工作", first.FolderName);
        Assert.Equal(updatedAt, first.UpdatedAt);
        var update = Assert.Single(repository.BatchFolderUpdates);
        Assert.Equal(new[] { "first" }, update.NoteIds);
        Assert.Equal("工作", update.FolderName);
        Assert.Equal(1, folderService.MoveTargetValidationCount);
    }

    [Fact]
    public async Task MoveNotesAsync_DoesNotChangeMemoryWhenPersistenceFails()
    {
        var repository = new InMemoryNoteRepository
        {
            BatchFolderUpdateException = new InvalidOperationException("boom")
        };
        var note = CreateNote(new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc), "note");
        var service = new NoteService(repository, new FakeFolderService("工作"), () => note.UpdatedAt);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MoveNotesAsync(new[] { note }, "工作"));

        Assert.Equal("未分类", note.FolderName);
    }

    [Fact]
    public async Task DeleteNotesAsync_UsesOneTimestampAndMarksMemoryAfterPersistence()
    {
        var repository = new InMemoryNoteRepository();
        var deletedAt = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
        var first = CreateNote(deletedAt.AddHours(-2), "first");
        var second = CreateNote(deletedAt.AddHours(-1), "second");
        var service = new NoteService(repository, () => deletedAt);

        await service.DeleteNotesAsync(new[] { first, first, second });

        Assert.All(new[] { first, second }, note =>
        {
            Assert.True(note.IsDeleted);
            Assert.Equal(deletedAt, note.DeletedAt);
        });
        Assert.Equal(deletedAt.AddHours(-2), first.UpdatedAt);
        Assert.Equal(deletedAt.AddHours(-1), second.UpdatedAt);
        var update = Assert.Single(repository.BatchDeleteUpdates);
        Assert.Equal(new[] { "first", "second" }, update.NoteIds);
        Assert.Equal(deletedAt, update.DeletedAt);
    }

    [Fact]
    public async Task BatchServiceOperations_SkipEmptyAndNoOpCollections()
    {
        var repository = new InMemoryNoteRepository();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var favorite = CreateNote(updatedAt, "favorite");
        favorite.IsFavorite = true;
        favorite.FavoritedAt = updatedAt.AddMinutes(-1);
        var service = new NoteService(repository, new FakeFolderService("未分类"), () => updatedAt.AddHours(1));

        await service.SetFavoritesAsync(new[] { favorite }, true);
        await service.MoveNotesAsync(new[] { favorite }, "未分类");
        await service.DeleteNotesAsync(Array.Empty<Note>());

        Assert.Empty(repository.BatchFavoriteUpdates);
        Assert.Empty(repository.BatchFolderUpdates);
        Assert.Empty(repository.BatchDeleteUpdates);
    }

    [Fact]
    public async Task GetRecentlyDeletedNotesAsync_UsesThirtyDayCutoff()
    {
        var now = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
        var repository = new InMemoryNoteRepository();
        repository.Notes.AddRange(new[]
        {
            CreateDeletedNote("boundary", now.AddDays(-30)),
            CreateDeletedNote("recent", now.AddDays(-1)),
            CreateDeletedNote("expired", now.AddDays(-30).AddTicks(-1))
        });
        var service = new NoteService(repository, () => now);

        var notes = await service.GetRecentlyDeletedNotesAsync();

        Assert.Equal(now.AddDays(-30), Assert.Single(repository.DeletedQueryCutoffs));
        Assert.Equal(new[] { "recent", "boundary" }, notes.Select(note => note.Id));
    }

    [Fact]
    public async Task RestoreNoteAsync_RecreatesMissingFolderAndPreservesEveryNoteProperty()
    {
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var deletedAt = updatedAt.AddDays(1);
        var favoritedAt = updatedAt.AddHours(-1);
        var note = CreateDeletedNote("note", deletedAt);
        note.Title = "标题";
        note.Content = "正文";
        note.CreatedAt = updatedAt.AddDays(-2);
        note.UpdatedAt = updatedAt;
        note.FolderName = "项目";
        note.IsFavorite = true;
        note.FavoritedAt = favoritedAt;
        var repository = new InMemoryNoteRepository();
        var folderService = new FakeFolderService();
        var service = new NoteService(repository, folderService, () => deletedAt.AddHours(1));

        await service.RestoreNoteAsync(note);

        Assert.Equal(new[] { "项目" }, folderService.CreatedFolders);
        Assert.Equal(new[] { "note" }, repository.RestoredIds);
        Assert.False(note.IsDeleted);
        Assert.Null(note.DeletedAt);
        Assert.Equal("标题", note.Title);
        Assert.Equal("正文", note.Content);
        Assert.Equal(updatedAt.AddDays(-2), note.CreatedAt);
        Assert.Equal(updatedAt, note.UpdatedAt);
        Assert.Equal("项目", note.FolderName);
        Assert.True(note.IsFavorite);
        Assert.Equal(favoritedAt, note.FavoritedAt);
    }

    [Fact]
    public async Task RestoreNoteAsync_KeepsDeletedStateWhenPersistenceFails()
    {
        var deletedAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var note = CreateDeletedNote("note", deletedAt);
        var repository = new InMemoryNoteRepository
        {
            RestoreException = new InvalidOperationException("boom")
        };
        var service = new NoteService(repository, new FakeFolderService("未分类"), () => deletedAt);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreNoteAsync(note));

        Assert.True(note.IsDeleted);
        Assert.Equal(deletedAt, note.DeletedAt);
    }

    [Fact]
    public async Task PermanentlyDeleteNoteAsync_DelegatesWithoutMutatingTheSnapshot()
    {
        var deletedAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var note = CreateDeletedNote("note", deletedAt);
        var repository = new InMemoryNoteRepository();
        var service = new NoteService(repository, () => deletedAt);

        await service.PermanentlyDeleteNoteAsync(note);

        Assert.Equal(new[] { "note" }, repository.PermanentlyDeletedIds);
        Assert.True(note.IsDeleted);
        Assert.Equal(deletedAt, note.DeletedAt);
    }

    private static Note CreateNote(DateTime updatedAt, string id = "note-1")
    {
        return new Note
        {
            Id = id,
            Title = "Title",
            Content = "Body",
            CreatedAt = updatedAt.AddMinutes(-1),
            UpdatedAt = updatedAt,
            FolderName = "未分类",
            IsDeleted = false
        };
    }

    private static Note CreateDeletedNote(string id, DateTime deletedAt)
    {
        var note = CreateNote(deletedAt.AddHours(-1), id);
        note.IsDeleted = true;
        note.DeletedAt = deletedAt;
        return note;
    }

    private sealed class InMemoryNoteRepository : INoteRepository
    {
        public List<Note> Notes { get; } = new();

        public List<Note> UpdatedNotes { get; } = new();

        public List<Note> FavoriteUpdates { get; } = new();

        public List<Note> FolderUpdates { get; } = new();

        public List<(string[] NoteIds, bool IsFavorite, DateTime? FavoritedAt)> BatchFavoriteUpdates { get; } = new();

        public List<(string[] NoteIds, string FolderName)> BatchFolderUpdates { get; } = new();

        public List<(string[] NoteIds, DateTime DeletedAt)> BatchDeleteUpdates { get; } = new();

        public List<DateTime> DeletedQueryCutoffs { get; } = new();

        public List<string> RestoredIds { get; } = new();

        public List<string> PermanentlyDeletedIds { get; } = new();

        public Exception? FavoriteUpdateException { get; init; }

        public Exception? FolderUpdateException { get; init; }

        public Exception? BatchFavoriteUpdateException { get; init; }

        public Exception? BatchFolderUpdateException { get; init; }

        public Exception? BatchDeleteUpdateException { get; init; }

        public Exception? RestoreException { get; init; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(Notes.Where(note => !note.IsDeleted).ToList());
        }

        public Task<IReadOnlyList<Note>> GetDeletedNotesAsync(
            DateTime deletedSince,
            CancellationToken cancellationToken = default)
        {
            DeletedQueryCutoffs.Add(deletedSince);
            return Task.FromResult<IReadOnlyList<Note>>(Notes
                .Where(note => note.IsDeleted && note.DeletedAt >= deletedSince)
                .OrderByDescending(note => note.DeletedAt)
                .ToList());
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
            if (BatchFavoriteUpdateException is not null)
            {
                throw BatchFavoriteUpdateException;
            }

            BatchFavoriteUpdates.Add((noteIds.ToArray(), isFavorite, favoritedAt));
            return Task.CompletedTask;
        }

        public Task MoveToFolderAsync(
            IReadOnlyCollection<string> noteIds,
            string folderName,
            CancellationToken cancellationToken = default)
        {
            if (BatchFolderUpdateException is not null)
            {
                throw BatchFolderUpdateException;
            }

            BatchFolderUpdates.Add((noteIds.ToArray(), folderName));
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
        {
            var note = Notes.Single(item => item.Id == noteId);
            note.IsDeleted = true;
            note.DeletedAt = deletedAt;
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(
            IReadOnlyCollection<string> noteIds,
            DateTime deletedAt,
            CancellationToken cancellationToken = default)
        {
            if (BatchDeleteUpdateException is not null)
            {
                throw BatchDeleteUpdateException;
            }

            BatchDeleteUpdates.Add((noteIds.ToArray(), deletedAt));
            return Task.CompletedTask;
        }

        public Task RestoreAsync(string noteId, CancellationToken cancellationToken = default)
        {
            if (RestoreException is not null)
            {
                throw RestoreException;
            }

            RestoredIds.Add(noteId);
            return Task.CompletedTask;
        }

        public Task PermanentlyDeleteAsync(string noteId, CancellationToken cancellationToken = default)
        {
            PermanentlyDeletedIds.Add(noteId);
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

        public int MoveTargetValidationCount { get; private set; }

        public List<string> CreatedFolders { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FolderSummary>>(_folderNames
                .Select(name => new FolderSummary(name, name, name == "未分类", 0, true))
                .ToArray());

        public Task<FolderSummary> CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            _folderNames.Add(name);
            CreatedFolders.Add(name);
            return Task.FromResult(new FolderSummary(name, name, false, 0, true));
        }

        public Task<FolderSummary> RenameAsync(FolderSummary folder, string newName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> IsValidMoveTargetAsync(string folderName, CancellationToken cancellationToken = default)
        {
            MoveTargetValidationCount++;
            return Task.FromResult(_folderNames.Contains(folderName));
        }
    }
}
