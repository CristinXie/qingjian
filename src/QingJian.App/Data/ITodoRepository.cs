using QingJian.App.Models;

namespace QingJian.App.Data;

public interface ITodoRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default);

    Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default);
}
