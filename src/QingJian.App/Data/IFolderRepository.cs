using QingJian.App.Models;

namespace QingJian.App.Data;

public interface IFolderRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FolderSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);

    Task<bool> NormalizedNameExistsAsync(
        string normalizedName,
        string? excludedFolderId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Folder folder, CancellationToken cancellationToken = default);

    Task RenameAsync(
        string folderId,
        string oldName,
        string newName,
        string normalizedName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string folderId,
        string folderName,
        CancellationToken cancellationToken = default);
}
