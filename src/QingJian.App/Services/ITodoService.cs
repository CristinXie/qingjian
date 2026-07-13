using QingJian.App.Models;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public interface ITodoService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default);

    Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default);

    Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default);

    Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default);

    IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos);
}
