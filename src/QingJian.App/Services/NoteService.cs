using QingJian.App.Data;
using QingJian.App.Models;

namespace QingJian.App.Services;

public sealed class NoteService : INoteService
{
    public const string DefaultTitle = "未命名文件";
    private readonly INoteRepository _noteRepository;
    private readonly IFolderService? _folderService;
    private readonly Func<DateTime> _utcNow;

    public NoteService(INoteRepository noteRepository)
        : this(noteRepository, null, () => DateTime.UtcNow)
    {
    }

    public NoteService(INoteRepository noteRepository, Func<DateTime> utcNow)
        : this(noteRepository, null, utcNow)
    {
    }

    public NoteService(INoteRepository noteRepository, IFolderService folderService)
        : this(noteRepository, folderService, () => DateTime.UtcNow)
    {
    }

    public NoteService(
        INoteRepository noteRepository,
        IFolderService? folderService,
        Func<DateTime> utcNow)
    {
        _noteRepository = noteRepository;
        _folderService = folderService;
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
        return await CreateNoteCoreAsync(FolderNamePolicy.UncategorizedName, cancellationToken);
    }

    public async Task<Note> CreateNoteInFolderAsync(
        string folderName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = await GetValidFolderNameAsync(folderName, cancellationToken);
        return await CreateNoteCoreAsync(normalizedName, cancellationToken);
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

    public async Task MoveNoteAsync(
        Note note,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = await GetValidFolderNameAsync(folderName, cancellationToken);
        var originalFolderName = note.FolderName;
        note.FolderName = normalizedName;

        try
        {
            await _noteRepository.UpdateFolderAsync(note, cancellationToken);
        }
        catch
        {
            note.FolderName = originalFolderName;
            throw;
        }
    }

    public async Task SetFavoritesAsync(
        IReadOnlyCollection<Note> notes,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        var changedNotes = NormalizeNotes(notes)
            .Where(note => note.IsFavorite != isFavorite)
            .ToArray();
        if (changedNotes.Length == 0)
        {
            return;
        }

        var favoritedAt = isFavorite ? _utcNow() : (DateTime?)null;
        await _noteRepository.SetFavoritesAsync(
            changedNotes.Select(note => note.Id).ToArray(),
            isFavorite,
            favoritedAt,
            cancellationToken);

        foreach (var note in changedNotes)
        {
            note.IsFavorite = isFavorite;
            note.FavoritedAt = favoritedAt;
        }
    }

    public async Task MoveNotesAsync(
        IReadOnlyCollection<Note> notes,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = await GetValidFolderNameAsync(folderName, cancellationToken);
        var changedNotes = NormalizeNotes(notes)
            .Where(note => !string.Equals(
                note.FolderName,
                normalizedName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (changedNotes.Length == 0)
        {
            return;
        }

        await _noteRepository.MoveToFolderAsync(
            changedNotes.Select(note => note.Id).ToArray(),
            normalizedName,
            cancellationToken);

        foreach (var note in changedNotes)
        {
            note.FolderName = normalizedName;
        }
    }

    public async Task DeleteNotesAsync(
        IReadOnlyCollection<Note> notes,
        CancellationToken cancellationToken = default)
    {
        var changedNotes = NormalizeNotes(notes)
            .Where(note => !note.IsDeleted)
            .ToArray();
        if (changedNotes.Length == 0)
        {
            return;
        }

        var deletedAt = _utcNow();
        await _noteRepository.SoftDeleteAsync(
            changedNotes.Select(note => note.Id).ToArray(),
            deletedAt,
            cancellationToken);

        foreach (var note in changedNotes)
        {
            note.IsDeleted = true;
            note.UpdatedAt = deletedAt;
        }
    }

    public Task DeleteNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        return _noteRepository.SoftDeleteAsync(note.Id, _utcNow(), cancellationToken);
    }

    private static Note[] NormalizeNotes(IReadOnlyCollection<Note> notes)
    {
        return notes
            .Where(note => note is not null && !string.IsNullOrWhiteSpace(note.Id))
            .DistinctBy(note => note.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<Note> CreateNoteCoreAsync(
        string folderName,
        CancellationToken cancellationToken)
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
            FolderName = folderName,
            IsDeleted = false
        };

        await _noteRepository.AddAsync(note, cancellationToken);
        return note;
    }

    private async Task<string> GetValidFolderNameAsync(
        string folderName,
        CancellationToken cancellationToken)
    {
        if (_folderService is null)
        {
            throw new InvalidOperationException("文件夹服务未配置。");
        }

        var normalizedName = FolderNamePolicy.NormalizeDisplayName(folderName);
        if (!await _folderService.IsValidMoveTargetAsync(normalizedName, cancellationToken))
        {
            throw new InvalidOperationException("目标文件夹不存在或名称无效。");
        }

        return normalizedName;
    }
}
