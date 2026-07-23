using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public sealed class TodoService : ITodoService
{
    private readonly ITodoRepository _repository;

    public TodoService(ITodoRepository repository)
    {
        _repository = repository;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _repository.InitializeAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TodoItem>> GetTodosAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var todos = await _repository.GetActiveTodosAsync(fromDate, toDate, cancellationToken);
        return SortTodos(todos);
    }

    public async Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var normalized = NormalizeDraft(draft);
        var todo = new TodoItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Date = normalized.Date,
            Text = normalized.Text.Trim(),
            StartTime = normalized.StartTime,
            EndTime = normalized.EndTime,
            CreatedAt = now,
            UpdatedAt = now,
            IsCompleted = false,
            IsDeleted = false
        };

        await _repository.AddAsync(todo, cancellationToken);
        return todo;
    }

    public async Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDraft(draft);
        todo.Date = normalized.Date;
        todo.Text = normalized.Text.Trim();
        todo.StartTime = normalized.StartTime;
        todo.EndTime = normalized.EndTime;
        todo.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(todo, cancellationToken);
    }

    public async Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default)
    {
        if (todo.IsCompleted == isCompleted)
        {
            return;
        }

        var now = DateTime.UtcNow;
        todo.IsCompleted = isCompleted;
        todo.CompletedAt = isCompleted ? now : null;
        todo.UpdatedAt = now;

        await _repository.UpdateAsync(todo, cancellationToken);
    }

    public Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        return _repository.SoftDeleteAsync(todo.Id, DateTime.UtcNow, cancellationToken);
    }

    public IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos)
    {
        return todos
            .OrderBy(todo => todo.Date)
            .ThenBy(todo => todo.IsCompleted)
            .ThenBy(todo => todo.IsCompleted ? 1 : todo.StartTime.HasValue ? 0 : 1)
            .ThenBy(todo => todo.IsCompleted ? null : todo.StartTime)
            .ThenBy(todo => todo.IsCompleted ? todo.CompletedAt : todo.CreatedAt)
            .ThenBy(todo => todo.UpdatedAt)
            .ToList();
    }

    private static TodoDraft NormalizeDraft(TodoDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Text))
        {
            throw new ArgumentException("Todo text cannot be empty.", nameof(draft));
        }

        return draft.TimeKind switch
        {
            TodoTimeKind.None => draft with { StartTime = null, EndTime = null },
            TodoTimeKind.Single when draft.StartTime is not null => draft with { EndTime = null },
            TodoTimeKind.Range when draft.StartTime is not null &&
                                    draft.EndTime is not null &&
                                    TodoTimeRangeRules.IsValidRange(
                                        draft.StartTime.Value,
                                        draft.EndTime.Value) => draft,
            TodoTimeKind.Single => throw new ArgumentException("Single-time todos require a start time.", nameof(draft)),
            TodoTimeKind.Range => throw new ArgumentException(
                "Time ranges require different start and end times.",
                nameof(draft)),
            _ => throw new ArgumentOutOfRangeException(nameof(draft), "Unsupported todo time kind.")
        };
    }
}
