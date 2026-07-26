using QingJian.App.Models;

namespace QingJian.App.Services;

public interface IFolderService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FolderSummary>> GetFoldersAsync(CancellationToken cancellationToken = default);

    Task<FolderSummary> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<FolderSummary> RenameAsync(
        FolderSummary folder,
        string newName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(FolderSummary folder, CancellationToken cancellationToken = default);

    Task<bool> IsValidMoveTargetAsync(string folderName, CancellationToken cancellationToken = default);
}
