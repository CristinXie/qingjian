using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;
using Xunit;

namespace QingJian.App.Tests.Data;

public sealed class FolderRepositoryInitializationTests
{
    [Fact]
    public async Task InitializeAsync_BackfillsAndCanonicalizesLegacyFolderNamesIdempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);

        var noteRepository = new NoteRepository(db);
        await noteRepository.InitializeAsync();
        db.Notes.AddRange(
            CreateNote("one", " 项目 "),
            CreateNote("two", "项目"),
            CreateNote("three", "全部便签"));
        await db.SaveChangesAsync();

        var repository = new FolderRepository(db);
        await repository.InitializeAsync();
        await repository.InitializeAsync();

        var folders = await db.Folders.OrderBy(folder => folder.Name).ToListAsync();

        Assert.Contains(folders, folder =>
            folder.Id == Folder.SystemUncategorizedId
            && folder.Name == "未分类"
            && folder.IsSystem);
        Assert.Single(folders, folder => folder.Name == "项目");
        Assert.Equal("项目", (await db.Notes.SingleAsync(note => note.Id == "one")).FolderName);
        Assert.Equal("项目", (await db.Notes.SingleAsync(note => note.Id == "two")).FolderName);
        Assert.Equal("未分类", (await db.Notes.SingleAsync(note => note.Id == "three")).FolderName);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static Note CreateNote(string id, string folderName)
    {
        var now = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);
        return new Note
        {
            Id = id,
            Title = id,
            Content = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            FolderName = folderName,
            IsDeleted = false
        };
    }
}
