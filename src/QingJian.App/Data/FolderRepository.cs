using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.Data;

public sealed class FolderRepository : IFolderRepository
{
    private readonly AppDbContext _dbContext;
    private readonly Func<CancellationToken, Task>? _transactionCheckpoint;

    public FolderRepository(
        AppDbContext dbContext,
        Func<CancellationToken, Task>? transactionCheckpoint = null)
    {
        _dbContext = dbContext;
        _transactionCheckpoint = transactionCheckpoint;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS Folders (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                NormalizedName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsSystem INTEGER NOT NULL DEFAULT 0
            );
            """,
            cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Folders_NormalizedName ON Folders (NormalizedName);",
            cancellationToken);

        var now = DateTime.UtcNow;
        var uncategorized = await _dbContext.Folders
            .SingleOrDefaultAsync(folder => folder.Id == Folder.SystemUncategorizedId, cancellationToken);
        if (uncategorized is null)
        {
            _dbContext.Folders.Add(new Folder
            {
                Id = Folder.SystemUncategorizedId,
                Name = FolderNamePolicy.UncategorizedName,
                NormalizedName = FolderNamePolicy.NormalizeKey(FolderNamePolicy.UncategorizedName),
                CreatedAt = now,
                IsSystem = true
            });
        }
        else
        {
            uncategorized.Name = FolderNamePolicy.UncategorizedName;
            uncategorized.NormalizedName = FolderNamePolicy.NormalizeKey(FolderNamePolicy.UncategorizedName);
            uncategorized.IsSystem = true;
        }

        var foldersByKey = await _dbContext.Folders
            .Where(folder => folder.Id != Folder.SystemUncategorizedId)
            .ToDictionaryAsync(folder => folder.NormalizedName, StringComparer.Ordinal, cancellationToken);
        var notes = await _dbContext.Notes.ToListAsync(cancellationToken);

        foreach (var note in notes.OrderBy(note => note.Id, StringComparer.Ordinal))
        {
            var displayName = FolderNamePolicy.NormalizeDisplayName(note.FolderName);
            if (displayName.Length == 0 || displayName == FolderNamePolicy.AllNotesName)
            {
                displayName = FolderNamePolicy.UncategorizedName;
            }

            if (!string.Equals(note.FolderName, displayName, StringComparison.Ordinal))
            {
                note.FolderName = displayName;
            }

            if (displayName == FolderNamePolicy.UncategorizedName)
            {
                continue;
            }

            var normalizedName = FolderNamePolicy.NormalizeKey(displayName);
            if (foldersByKey.ContainsKey(normalizedName))
            {
                var canonicalName = foldersByKey[normalizedName].Name;
                if (!string.Equals(note.FolderName, canonicalName, StringComparison.Ordinal))
                {
                    note.FolderName = canonicalName;
                }

                continue;
            }

            var folder = new Folder
            {
                Name = displayName,
                NormalizedName = normalizedName,
                CreatedAt = now,
                IsSystem = false
            };
            _dbContext.Folders.Add(folder);
            foldersByKey.Add(normalizedName, folder);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FolderSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _dbContext.Notes
            .Where(note => !note.IsDeleted)
            .GroupBy(note => note.FolderName)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Name, item => item.Count, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var folders = await _dbContext.Folders
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return folders
            .Select(folder => new FolderSummary(
                folder.Id,
                folder.Name,
                folder.IsSystem,
                counts.TryGetValue(folder.Name, out var count) ? count : 0,
                folder.IsSystem || FolderNamePolicy.IsValidMoveTarget(folder.Name)))
            .OrderBy(folder => folder.IsSystem ? 0 : 1)
            .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public Task<bool> NormalizedNameExistsAsync(
        string normalizedName,
        string? excludedFolderId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Folders.AnyAsync(
            folder => folder.NormalizedName == normalizedName
                && (excludedFolderId == null || folder.Id != excludedFolderId),
            cancellationToken);
    }

    public async Task AddAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        _dbContext.Folders.Add(folder);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameAsync(
        string folderId,
        string oldName,
        string newName,
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var folder = await _dbContext.Folders.SingleAsync(item => item.Id == folderId, cancellationToken);
        if (folder.IsSystem)
        {
            throw new InvalidOperationException("未分类文件夹不能重命名。");
        }

        var notes = await _dbContext.Notes
            .Where(note => note.FolderName == oldName)
            .ToListAsync(cancellationToken);
        foreach (var note in notes)
        {
            note.FolderName = newName;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_transactionCheckpoint is not null)
        {
            await _transactionCheckpoint(cancellationToken);
        }

        folder.Name = newName;
        folder.NormalizedName = normalizedName;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string folderId,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var folder = await _dbContext.Folders.SingleAsync(item => item.Id == folderId, cancellationToken);
        if (folder.IsSystem)
        {
            throw new InvalidOperationException("未分类文件夹不能删除。");
        }

        var notes = await _dbContext.Notes
            .Where(note => note.FolderName == folderName)
            .ToListAsync(cancellationToken);
        foreach (var note in notes)
        {
            note.FolderName = FolderNamePolicy.UncategorizedName;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (_transactionCheckpoint is not null)
        {
            await _transactionCheckpoint(cancellationToken);
        }

        _dbContext.Folders.Remove(folder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
