using QingJian.App.Models;

namespace QingJian.App.Data;

public interface INoteRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);

    Task UpdateFavoriteAsync(Note note, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default);
}
