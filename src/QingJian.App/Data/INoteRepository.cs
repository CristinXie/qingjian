using QingJian.App.Models;

namespace QingJian.App.Data;

public interface INoteRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateFavoriteAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateFolderAsync(Note note, CancellationToken cancellationToken = default);

    Task SetFavoritesAsync(
        IReadOnlyCollection<string> noteIds,
        bool isFavorite,
        DateTime? favoritedAt,
        CancellationToken cancellationToken = default);

    Task MoveToFolderAsync(
        IReadOnlyCollection<string> noteIds,
        string folderName,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(
        IReadOnlyCollection<string> noteIds,
        DateTime deletedAt,
        CancellationToken cancellationToken = default);
}
