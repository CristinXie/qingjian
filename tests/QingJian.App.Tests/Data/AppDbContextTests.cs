using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;
using Xunit;

namespace QingJian.App.Tests.Data;

public sealed class AppDbContextTests
{
    [Fact]
    public async Task EnsureCreated_CreatesNotesTableAndPersistsNote()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var favoritedAt = new DateTime(2026, 7, 8, 10, 5, 0, DateTimeKind.Utc);

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Notes.Add(new Note
            {
                Id = "note-1",
                Title = "First note",
                Content = "Hello",
                CreatedAt = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc),
                IsFavorite = true,
                FavoritedAt = favoritedAt,
                FolderName = "项目",
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new AppDbContext(options);
        var saved = await readDb.Notes.SingleAsync();

        Assert.Equal("note-1", saved.Id);
        Assert.Equal("First note", saved.Title);
        Assert.Equal("Hello", saved.Content);
        Assert.True(saved.IsFavorite);
        Assert.Equal(favoritedAt, saved.FavoritedAt);
        Assert.Equal("项目", saved.FolderName);
        Assert.False(saved.IsDeleted);
    }
}
