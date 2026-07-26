using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.Services;
using Xunit;

namespace QingJian.App.Tests.Data;

public sealed class FolderRepositoryTests
{
    [Fact]
    public async Task GetSummariesAsync_ReturnsActiveCountsAndKeepsEmptyFolders()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        var repository = await InitializeAsync(db);
        var folder = await AddFolderAsync(repository, "项目");

        db.Notes.AddRange(
            CreateNote("active", "项目", false),
            CreateNote("deleted", "项目", true));
        await db.SaveChangesAsync();

        var summaries = await repository.GetSummariesAsync();

        Assert.Equal(0, summaries.Single(item => item.Name == "未分类").NoteCount);
        Assert.Equal(1, summaries.Single(item => item.Id == folder.Id).NoteCount);
        Assert.True(summaries.Single(item => item.Id == folder.Id).IsValidMoveTarget);
    }

    [Fact]
    public async Task NormalizedNameExistsAsync_AllowsTheFolderItselfAsTheExcludedId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        var repository = await InitializeAsync(db);
        var folder = await AddFolderAsync(repository, "项目");
        var key = FolderNamePolicy.NormalizeKey("项目");

        Assert.True(await repository.NormalizedNameExistsAsync(key));
        Assert.False(await repository.NormalizedNameExistsAsync(key, folder.Id));
    }

    [Fact]
    public async Task RenameAsync_UpdatesActiveAndDeletedNotesInOneOperation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        var repository = await InitializeAsync(db);
        var folder = await AddFolderAsync(repository, "项目");
        db.Notes.AddRange(
            CreateNote("active", "项目", false),
            CreateNote("deleted", "项目", true));
        await db.SaveChangesAsync();

        await repository.RenameAsync(folder.Id, "项目", "工作", FolderNamePolicy.NormalizeKey("工作"));
        db.ChangeTracker.Clear();

        var notes = await db.Notes.OrderBy(note => note.Id).ToListAsync();
        Assert.All(notes, note => Assert.Equal("工作", note.FolderName));
        var renamed = await db.Folders.SingleAsync(item => item.Id == folder.Id);
        Assert.Equal("工作", renamed.Name);
        Assert.Equal(FolderNamePolicy.NormalizeKey("工作"), renamed.NormalizedName);
    }

    [Fact]
    public async Task DeleteAsync_MovesAllNotesToUncategorizedAndRemovesFolder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        var repository = await InitializeAsync(db);
        var folder = await AddFolderAsync(repository, "项目");
        db.Notes.AddRange(
            CreateNote("active", "项目", false),
            CreateNote("deleted", "项目", true));
        await db.SaveChangesAsync();

        await repository.DeleteAsync(folder.Id, "项目");
        db.ChangeTracker.Clear();

        Assert.All(await db.Notes.ToListAsync(), note => Assert.Equal("未分类", note.FolderName));
        Assert.False(await db.Folders.AnyAsync(item => item.Id == folder.Id));
    }

    [Fact]
    public async Task RenameAsync_RollsBackNoteAndFolderChangesWhenCheckpointFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        var initialRepository = await InitializeAsync(db);
        var folder = await AddFolderAsync(initialRepository, "项目");
        db.Notes.Add(CreateNote("note", "项目", false));
        await db.SaveChangesAsync();

        var repository = new FolderRepository(
            db,
            _ => throw new InvalidOperationException("checkpoint"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.RenameAsync(folder.Id, "项目", "工作", FolderNamePolicy.NormalizeKey("工作")));

        db.ChangeTracker.Clear();
        Assert.Equal("项目", (await db.Notes.SingleAsync()).FolderName);
        Assert.Equal("项目", (await db.Folders.SingleAsync(item => item.Id == folder.Id)).Name);
    }

    private static async Task<FolderRepository> InitializeAsync(AppDbContext db)
    {
        var repository = new FolderRepository(db);
        await repository.InitializeAsync();
        return repository;
    }

    private static async Task<Folder> AddFolderAsync(FolderRepository repository, string name)
    {
        var folder = new Folder
        {
            Name = name,
            NormalizedName = FolderNamePolicy.NormalizeKey(name),
            CreatedAt = new DateTime(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc)
        };
        await repository.AddAsync(folder);
        return folder;
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static Note CreateNote(string id, string folderName, bool isDeleted)
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
            IsDeleted = isDeleted
        };
    }
}
