using QingJian.App.Data;
using QingJian.App.Models;

namespace QingJian.App.Services;

public sealed class NoteService : INoteService
{
    public const string DefaultTitle = "未命名文件";
    private readonly INoteRepository _noteRepository;
    private readonly Func<DateTime> _utcNow;

    public NoteService(INoteRepository noteRepository)
        : this(noteRepository, () => DateTime.UtcNow)
    {
    }

    public NoteService(INoteRepository noteRepository, Func<DateTime> utcNow)
    {
        _noteRepository = noteRepository;
        _utcNow = utcNow;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _noteRepository.InitializeAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
    {
        return _noteRepository.GetActiveNotesAsync(cancellationToken);
    }

    public async Task<Note> CreateNoteAsync(CancellationToken cancellationToken = default)
    {
        var now = _utcNow();
        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = DefaultTitle,
            Content = string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            IsFavorite = false,
            FavoritedAt = null,
            FolderName = "未分类",
            IsDeleted = false
        };

        await _noteRepository.AddAsync(note, cancellationToken);
        return note;
    }

    public Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        note.Title = string.IsNullOrWhiteSpace(note.Title) ? DefaultTitle : note.Title.Trim();
        note.UpdatedAt = _utcNow();
        return _noteRepository.UpdateAsync(note, cancellationToken);
    }

    public async Task SetFavoriteAsync(Note note, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var originalIsFavorite = note.IsFavorite;
        var originalFavoritedAt = note.FavoritedAt;

        note.IsFavorite = isFavorite;
        note.FavoritedAt = isFavorite ? _utcNow() : null;

        try
        {
            await _noteRepository.UpdateFavoriteAsync(note, cancellationToken);
        }
        catch
        {
            note.IsFavorite = originalIsFavorite;
            note.FavoritedAt = originalFavoritedAt;
            throw;
        }
    }

    public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        return _noteRepository.SoftDeleteAsync(note.Id, _utcNow(), cancellationToken);
    }
}
