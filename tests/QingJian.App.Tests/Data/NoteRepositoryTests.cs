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

    private static NoteRepository CreateRepository(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new NoteRepository(new AppDbContext(options));
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
}
