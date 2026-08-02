using System.Data;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class NoteRepository : INoteRepository
{
    private readonly AppDbContext _dbContext;

    public NoteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var columns = await GetNoteColumnNamesAsync(cancellationToken);

        if (!columns.Contains("IsFavorite"))
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Notes ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }

        if (!columns.Contains("FavoritedAt"))
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Notes ADD COLUMN FavoritedAt TEXT NULL;",
                cancellationToken);
        }

        if (!columns.Contains("FolderName"))
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Notes ADD COLUMN FolderName TEXT NOT NULL DEFAULT '未分类';",
                cancellationToken);
        }

        if (!columns.Contains("DeletedAt"))
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Notes ADD COLUMN DeletedAt TEXT NULL;",
                cancellationToken);
        }

        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE Notes SET FolderName = '未分类' WHERE FolderName IS NULL OR trim(FolderName) = '';",
            cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE Notes SET DeletedAt = UpdatedAt WHERE IsDeleted = 1 AND DeletedAt IS NULL;",
            cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notes
            .Where(note => !note.IsDeleted)
            .OrderByDescending(note => note.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetDeletedNotesAsync(
        DateTime deletedSince,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notes
            .Where(note => note.IsDeleted
                && note.DeletedAt != null
                && note.DeletedAt >= deletedSince)
            .OrderByDescending(note => note.DeletedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        _dbContext.Notes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(item => item.Id == note.Id, cancellationToken);

        existing.Title = note.Title;
        existing.Content = note.Content;
        existing.UpdatedAt = note.UpdatedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFavoriteAsync(Note note, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(item => item.Id == note.Id, cancellationToken);

        existing.IsFavorite = note.IsFavorite;
        existing.FavoritedAt = note.FavoritedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFolderAsync(Note note, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(item => item.Id == note.Id, cancellationToken);

        existing.FolderName = note.FolderName;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetFavoritesAsync(
        IReadOnlyCollection<string> noteIds,
        bool isFavorite,
        DateTime? favoritedAt,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(noteIds);
        if (ids.Length == 0)
        {
            return;
        }

        await _dbContext.Notes
            .Where(note => ids.Contains(note.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(note => note.IsFavorite, isFavorite)
                .SetProperty(note => note.FavoritedAt, favoritedAt), cancellationToken);
    }

    public async Task MoveToFolderAsync(
        IReadOnlyCollection<string> noteIds,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(noteIds);
        if (ids.Length == 0)
        {
            return;
        }

        await _dbContext.Notes
            .Where(note => ids.Contains(note.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(note => note.FolderName, folderName), cancellationToken);
    }

    public async Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(note => note.Id == noteId, cancellationToken);

        existing.IsDeleted = true;
        existing.DeletedAt = deletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(
        IReadOnlyCollection<string> noteIds,
        DateTime deletedAt,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeIds(noteIds);
        if (ids.Length == 0)
        {
            return;
        }

        await _dbContext.Notes
            .Where(note => ids.Contains(note.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(note => note.IsDeleted, true)
                .SetProperty(note => note.DeletedAt, deletedAt), cancellationToken);
    }

    public async Task RestoreAsync(string noteId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notes
            .Where(note => note.Id == noteId && note.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(note => note.IsDeleted, false)
                .SetProperty(note => note.DeletedAt, (DateTime?)null), cancellationToken);

        foreach (var entry in _dbContext.ChangeTracker.Entries<Note>()
                     .Where(entry => entry.Entity.Id == noteId))
        {
            var isDeleted = entry.Property(note => note.IsDeleted);
            isDeleted.CurrentValue = false;
            isDeleted.OriginalValue = false;
            var deletedAt = entry.Property(note => note.DeletedAt);
            deletedAt.CurrentValue = null;
            deletedAt.OriginalValue = null;
        }
    }

    public async Task PermanentlyDeleteAsync(
        string noteId,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Notes
            .Where(note => note.Id == noteId && note.IsDeleted)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var entry in _dbContext.ChangeTracker.Entries<Note>()
                     .Where(entry => entry.Entity.Id == noteId)
                     .ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static string[] NormalizeIds(IReadOnlyCollection<string> noteIds)
    {
        return noteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<HashSet<string>> GetNoteColumnNamesAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(\"Notes\");";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
