using QingJian.App.Models;

namespace QingJian.App.Services;

public interface INoteService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default);

    Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default);

    Task<Note> CreateNoteInFolderAsync(string folderName, CancellationToken cancellationToken = default);

    Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(Note note, bool isFavorite, CancellationToken cancellationToken = default);

    Task MoveNoteAsync(Note note, string folderName, CancellationToken cancellationToken = default);

    Task SetFavoritesAsync(
        IReadOnlyCollection<Note> notes,
        bool isFavorite,
        CancellationToken cancellationToken = default);

    Task MoveNotesAsync(
        IReadOnlyCollection<Note> notes,
        string folderName,
        CancellationToken cancellationToken = default);

    Task DeleteNotesAsync(
        IReadOnlyCollection<Note> notes,
        CancellationToken cancellationToken = default);

    Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default);
}
