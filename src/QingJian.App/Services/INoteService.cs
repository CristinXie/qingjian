using QingJian.App.Models;

namespace QingJian.App.Services;

public interface INoteService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default);

    Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default);

    Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default);

    Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default);
}
