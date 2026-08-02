using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;
using Xunit;

namespace QingJian.App.Tests.Data;

public sealed class NoteRepositoryTests
{
    [Fact]
    public async Task GetActiveNotesAsync_ReturnsOnlyNonDeletedNotesOrderedByUpdatedAtDescending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        await repository.AddAsync(CreateNote("old", "Old", new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc), false));
        await repository.AddAsync(CreateNote("new", "New", new DateTime(2026, 7, 8, 9, 0, 0, DateTimeKind.Utc), false));
        await repository.AddAsync(CreateNote("deleted", "Deleted", new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc), true));

        var notes = await repository.GetActiveNotesAsync();

        Assert.Collection(
            notes,
            note => Assert.Equal("new", note.Id),
            note => Assert.Equal("old", note.Id));
    }

    [Fact]
    public async Task SoftDeleteAsync_HidesNoteFromActiveResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        await repository.AddAsync(CreateNote("note-1", "Visible", new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc), false));

        await repository.SoftDeleteAsync("note-1", new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc));

        var notes = await repository.GetActiveNotesAsync();
        Assert.Empty(notes);
    }

    [Fact]
    public async Task InitializeAsync_AddsMetadataColumnsAndBackfillsLegacyDeletionTime()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateLegacyNotesTableAsync(connection);
        await InsertLegacyNoteAsync(connection, "legacy");
        await InsertLegacyNoteAsync(connection, "legacy-deleted", isDeleted: true);
        var repository = CreateRepository(connection);

        await repository.InitializeAsync();
        await repository.InitializeAsync();

        var columns = await ReadColumnNamesAsync(connection, "Notes");
        Assert.Contains("IsFavorite", columns);
        Assert.Contains("FavoritedAt", columns);
        Assert.Contains("FolderName", columns);
        Assert.Contains("DeletedAt", columns);

        var note = Assert.Single(await repository.GetActiveNotesAsync());
        Assert.False(note.IsFavorite);
        Assert.Null(note.FavoritedAt);
        Assert.Equal("未分类", note.FolderName);
        var deleted = Assert.Single(await repository.GetDeletedNotesAsync(DateTime.MinValue));
        Assert.Equal(new DateTime(2026, 7, 20, 9, 0, 0), deleted.DeletedAt);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_PersistsFavoriteWithoutChangingContentOrUpdatedAt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var favoritedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var note = CreateNote("note-1", "Title", updatedAt, false);
        note.Content = "Body";
        await repository.AddAsync(note);

        note.IsFavorite = true;
        note.FavoritedAt = favoritedAt;
        await repository.UpdateFavoriteAsync(note);

        var loaded = Assert.Single(await repository.GetActiveNotesAsync());
        Assert.True(loaded.IsFavorite);
        Assert.Equal(favoritedAt, loaded.FavoritedAt);
        Assert.Equal("Body", loaded.Content);
        Assert.Equal(updatedAt, loaded.UpdatedAt);
    }

    [Fact]
    public async Task UpdateFolderAsync_PersistsFolderWithoutChangingUpdatedAt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var note = CreateNote("note-1", "Title", updatedAt, false);
        await repository.AddAsync(note);

        note.FolderName = "项目";
        await repository.UpdateFolderAsync(note);

        var loaded = Assert.Single(await repository.GetActiveNotesAsync());
        Assert.Equal("项目", loaded.FolderName);
        Assert.Equal(updatedAt, loaded.UpdatedAt);
    }

    [Fact]
    public async Task SetFavoritesAsync_UpdatesDistinctMatchesWithoutChangingOtherFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var favoritedAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var first = CreateNote("first", "First", updatedAt, false);
        first.Content = "First body";
        first.FolderName = "项目";
        var second = CreateNote("second", "Second", updatedAt.AddMinutes(1), false);
        var untouched = CreateNote("untouched", "Untouched", updatedAt.AddMinutes(2), false);
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.AddAsync(untouched);

        await repository.SetFavoritesAsync(
            new[] { "first", "first", "second", "missing" },
            true,
            favoritedAt);

        await using var context = CreateContext(connection);
        var loaded = await context.Notes.OrderBy(note => note.Id).ToArrayAsync();
        Assert.True(loaded.Single(note => note.Id == "first").IsFavorite);
        Assert.Equal(favoritedAt, loaded.Single(note => note.Id == "first").FavoritedAt);
        Assert.Equal(updatedAt, loaded.Single(note => note.Id == "first").UpdatedAt);
        Assert.Equal("First body", loaded.Single(note => note.Id == "first").Content);
        Assert.Equal("项目", loaded.Single(note => note.Id == "first").FolderName);
        Assert.True(loaded.Single(note => note.Id == "second").IsFavorite);
        Assert.False(loaded.Single(note => note.Id == "untouched").IsFavorite);
    }

    [Fact]
    public async Task MoveToFolderAsync_UpdatesDistinctMatchesWithoutChangingUpdatedAt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await repository.AddAsync(CreateNote("first", "First", updatedAt, false));
        await repository.AddAsync(CreateNote("second", "Second", updatedAt.AddMinutes(1), false));

        await repository.MoveToFolderAsync(new[] { "first", "first", "missing" }, "工作");

        await using var context = CreateContext(connection);
        var first = await context.Notes.SingleAsync(note => note.Id == "first");
        var second = await context.Notes.SingleAsync(note => note.Id == "second");
        Assert.Equal("工作", first.FolderName);
        Assert.Equal(updatedAt, first.UpdatedAt);
        Assert.Equal("未分类", second.FolderName);
    }

    [Fact]
    public async Task BatchSoftDeleteAsync_UsesOneTimestampAndPreservesModificationMetadata()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var deletedAt = updatedAt.AddHours(1);
        await repository.AddAsync(CreateNote("first", "First", updatedAt, false));
        await repository.AddAsync(CreateNote("second", "Second", updatedAt.AddMinutes(1), false));
        await repository.AddAsync(CreateNote("untouched", "Untouched", updatedAt.AddMinutes(2), false));

        await repository.SoftDeleteAsync(new[] { "first", "first", "second", "missing" }, deletedAt);
        await repository.SetFavoritesAsync(Array.Empty<string>(), true, deletedAt);
        await repository.MoveToFolderAsync(Array.Empty<string>(), "工作");
        await repository.SoftDeleteAsync(Array.Empty<string>(), deletedAt.AddHours(1));

        await using var context = CreateContext(connection);
        var loaded = await context.Notes.OrderBy(note => note.Id).ToArrayAsync();
        Assert.All(loaded.Where(note => note.Id is "first" or "second"), note =>
        {
            Assert.True(note.IsDeleted);
            Assert.Equal(deletedAt, note.DeletedAt);
        });
        Assert.Equal(updatedAt, loaded.Single(note => note.Id == "first").UpdatedAt);
        Assert.Equal(updatedAt.AddMinutes(1), loaded.Single(note => note.Id == "second").UpdatedAt);
        var untouched = loaded.Single(note => note.Id == "untouched");
        Assert.False(untouched.IsDeleted);
        Assert.Null(untouched.DeletedAt);
        Assert.Equal(updatedAt.AddMinutes(2), untouched.UpdatedAt);
    }

    [Fact]
    public async Task GetDeletedNotesAsync_IncludesExactCutoffAndOrdersByDeletionTimeDescending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var cutoff = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
        await repository.AddAsync(CreateNote("boundary", "Boundary", cutoff.AddDays(-1), false));
        await repository.AddAsync(CreateNote("recent", "Recent", cutoff, false));
        await repository.AddAsync(CreateNote("expired", "Expired", cutoff, false));
        await repository.SoftDeleteAsync("boundary", cutoff);
        await repository.SoftDeleteAsync("recent", cutoff.AddHours(2));
        await repository.SoftDeleteAsync("expired", cutoff.AddTicks(-1));

        var notes = await repository.GetDeletedNotesAsync(cutoff);

        Assert.Equal(new[] { "recent", "boundary" }, notes.Select(note => note.Id));
    }

    [Fact]
    public async Task RestoreAndPermanentDelete_ChangeOnlyTheRequestedDeletionLifecycle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var deletedAt = updatedAt.AddHours(2);
        var restore = CreateNote("restore", "Restore", updatedAt, false);
        restore.Content = "Body";
        restore.FolderName = "项目";
        restore.IsFavorite = true;
        restore.FavoritedAt = updatedAt.AddHours(1);
        await repository.AddAsync(restore);
        await repository.AddAsync(CreateNote("purge", "Purge", updatedAt, false));
        await repository.AddAsync(CreateNote("active", "Active", updatedAt.AddMinutes(1), false));
        await repository.AddAsync(CreateNote("deleted-survivor", "Deleted survivor", updatedAt.AddMinutes(2), false));
        await repository.SoftDeleteAsync("restore", deletedAt);
        await repository.SoftDeleteAsync("purge", deletedAt);
        await repository.SoftDeleteAsync("deleted-survivor", deletedAt.AddMinutes(1));

        await repository.RestoreAsync("restore");
        await repository.PermanentlyDeleteAsync("purge");

        await using var context = CreateContext(connection);
        var restored = await context.Notes.SingleAsync(note => note.Id == "restore");
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Equal("Body", restored.Content);
        Assert.Equal(updatedAt, restored.UpdatedAt);
        Assert.Equal("项目", restored.FolderName);
        Assert.True(restored.IsFavorite);
        Assert.Equal(updatedAt.AddHours(1), restored.FavoritedAt);
        Assert.False(await context.Notes.AnyAsync(note => note.Id == "purge"));
        Assert.Equal(
            updatedAt.AddMinutes(1),
            (await context.Notes.SingleAsync(note => note.Id == "active")).UpdatedAt);
        Assert.Equal(
            updatedAt.AddMinutes(2),
            (await context.Notes.SingleAsync(note => note.Id == "deleted-survivor")).UpdatedAt);
    }

    [Fact]
    public async Task DeleteRestoreDelete_SynchronizesTrackedDeletionState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        var firstDeletion = updatedAt.AddHours(1);
        var secondDeletion = updatedAt.AddHours(2);
        await repository.AddAsync(CreateNote("note", "Note", updatedAt, false));

        await repository.SoftDeleteAsync("note", firstDeletion);
        await repository.RestoreAsync("note");
        await repository.SoftDeleteAsync("note", secondDeletion);

        await using var context = CreateContext(connection);
        var note = await context.Notes.AsNoTracking().SingleAsync();
        Assert.True(note.IsDeleted);
        Assert.Equal(secondDeletion, note.DeletedAt);
    }

    [Fact]
    public async Task PermanentlyDeleteAsync_DetachesTrackedEntity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();
        var updatedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        await repository.AddAsync(CreateNote("note", "Original", updatedAt, false));
        await repository.SoftDeleteAsync("note", updatedAt.AddHours(1));

        await repository.PermanentlyDeleteAsync("note");
        await repository.AddAsync(CreateNote("note", "Replacement", updatedAt.AddHours(2), false));

        var loaded = Assert.Single(await repository.GetActiveNotesAsync());
        Assert.Equal("Replacement", loaded.Title);
    }

    private static NoteRepository CreateRepository(SqliteConnection connection)
    {
        return new NoteRepository(CreateContext(connection));
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static Note CreateNote(string id, string title, DateTime updatedAt, bool isDeleted)
    {
        return new Note
        {
            Id = id,
            Title = title,
            Content = string.Empty,
            CreatedAt = updatedAt.AddMinutes(-1),
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted
        };
    }

    private static async Task CreateLegacyNotesTableAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Notes (
                Id TEXT NOT NULL PRIMARY KEY,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertLegacyNoteAsync(
        SqliteConnection connection,
        string id,
        bool isDeleted = false)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Notes (Id, Title, Content, CreatedAt, UpdatedAt, IsDeleted)
            VALUES ($id, 'Legacy', 'Body', '2026-07-20 08:00:00', '2026-07-20 09:00:00', $isDeleted);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$isDeleted", isDeleted);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }
}
