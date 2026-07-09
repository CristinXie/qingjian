using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class NoteRepository : INoteRepository
{
    private readonly AppDbContext _dbContext;

    public NoteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> GetActiveNotesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notes
            .Where(note => !note.IsDeleted)
            .OrderByDescending(note => note.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        _dbContext.Notes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(item => item.Id == note.Id, cancellationToken);

        existing.Title = note.Title;
        existing.Content = note.Content;
        existing.UpdatedAt = note.UpdatedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(string noteId, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Notes
            .SingleAsync(note => note.Id == noteId, cancellationToken);

        existing.IsDeleted = true;
        existing.UpdatedAt = deletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
