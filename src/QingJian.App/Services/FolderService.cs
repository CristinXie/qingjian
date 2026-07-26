using QingJian.App.Data;
using QingJian.App.Models;

namespace QingJian.App.Services;

public sealed class FolderService : IFolderService
{
    private readonly IFolderRepository _repository;
    private readonly Func<DateTime> _utcNow;

    public FolderService(IFolderRepository repository)
        : this(repository, () => DateTime.UtcNow)
    {
    }

    public FolderService(IFolderRepository repository, Func<DateTime> utcNow)
    {
        _repository = repository;
        _utcNow = utcNow;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _repository.InitializeAsync(cancellationToken);
    }

    public Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetSummariesAsync(cancellationToken);
    }

    public async Task<FolderSummary> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var displayName = ValidateName(name, allowUncategorized: false);
        var normalizedName = FolderNamePolicy.NormalizeKey(displayName);
        if (await _repository.NormalizedNameExistsAsync(normalizedName, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException("文件夹名称已存在。");
        }

        var folder = new Folder
        {
            Name = displayName,
            NormalizedName = normalizedName,
            CreatedAt = _utcNow(),
            IsSystem = false
        };
        await _repository.AddAsync(folder, cancellationToken);

        return new FolderSummary(
            folder.Id,
            folder.Name,
            false,
            0,
            FolderNamePolicy.IsValidMoveTarget(folder.Name));
    }

    public async Task<FolderSummary> RenameAsync(
        FolderSummary folder,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (folder.IsSystem)
        {
            throw new InvalidOperationException("未分类文件夹不能重命名。");
        }

        var displayName = ValidateName(newName, allowUncategorized: false);
        var normalizedName = FolderNamePolicy.NormalizeKey(displayName);
        if (!string.Equals(normalizedName, FolderNamePolicy.NormalizeKey(folder.Name), StringComparison.Ordinal)
            && await _repository.NormalizedNameExistsAsync(
                normalizedName,
                folder.Id,
                cancellationToken))
        {
            throw new InvalidOperationException("文件夹名称已存在。");
        }

        if (!string.Equals(folder.Name, displayName, StringComparison.Ordinal))
        {
            await _repository.RenameAsync(
                folder.Id,
                folder.Name,
                displayName,
                normalizedName,
                cancellationToken);
        }

        return folder with
        {
            Name = displayName,
            IsValidMoveTarget = FolderNamePolicy.IsValidMoveTarget(displayName)
        };
    }

    public Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default)
    {
        if (folder.IsSystem)
        {
            throw new InvalidOperationException("未分类文件夹不能删除。");
        }

        return _repository.DeleteAsync(folder.Id, folder.Name, cancellationToken);
    }

    public async Task<bool> IsValidMoveTargetAsync(
        string folderName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = FolderNamePolicy.NormalizeKey(folderName);
        var folders = await _repository.GetSummariesAsync(cancellationToken);
        return folders.Any(folder =>
            folder.IsValidMoveTarget
            && string.Equals(
                FolderNamePolicy.NormalizeKey(folder.Name),
                normalizedName,
                StringComparison.Ordinal));
    }

    private static string ValidateName(string name, bool allowUncategorized)
    {
        var displayName = FolderNamePolicy.NormalizeDisplayName(name);
        var error = FolderNamePolicy.GetValidationError(displayName, allowUncategorized);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        return displayName;
    }
}
