using Microsoft.EntityFrameworkCore;
using QingJian.App.Models;

namespace QingJian.App.Data;

public sealed class TodoRepository : ITodoRepository
{
    private readonly AppDbContext _dbContext;

    public TodoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return InitializeSchemaAsync(cancellationToken);
    }

    private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TodoItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TodoItems" PRIMARY KEY,
                "Date" TEXT NOT NULL,
                "Text" TEXT NOT NULL,
                "StartTime" TEXT NULL,
                "EndTime" TEXT NULL,
                "IsCompleted" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "CompletedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL DEFAULT 0
            );
            """,
            cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_TodoItems_Date_IsDeleted"
            ON "TodoItems" ("Date", "IsDeleted");
            """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TodoItems
            .Where(todo => !todo.IsDeleted && todo.Date >= fromDate && todo.Date <= toDate)
            .OrderBy(todo => todo.Date)
            .ThenBy(todo => todo.IsCompleted)
            .ThenBy(todo => todo.CreatedAt)
            .ThenBy(todo => todo.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        _dbContext.TodoItems.Add(todo);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.TodoItems
            .SingleAsync(item => item.Id == todo.Id, cancellationToken);

        existing.Date = todo.Date;
        existing.Text = todo.Text;
        existing.StartTime = todo.StartTime;
        existing.EndTime = todo.EndTime;
        existing.IsCompleted = todo.IsCompleted;
        existing.UpdatedAt = todo.UpdatedAt;
        existing.CompletedAt = todo.CompletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.TodoItems
            .SingleAsync(todo => todo.Id == todoId, cancellationToken);

        existing.IsDeleted = true;
        existing.UpdatedAt = deletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
