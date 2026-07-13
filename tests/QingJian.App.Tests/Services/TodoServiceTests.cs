using QingJian.App.Data;
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class TodoServiceTests
{
    [Fact]
    public async Task CreateTodoAsync_RejectsEmptyText()
    {
        var service = new TodoService(new InMemoryTodoRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateTodoAsync(new TodoDraft(
                new DateOnly(2026, 7, 13),
                "   ",
                TodoTimeKind.None,
                null,
                null)));
    }

    [Fact]
    public async Task CreateTodoAsync_CreatesNoTimeSingleTimeAndRangeTodos()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository);

        var none = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "No time", TodoTimeKind.None, null, null));
        var single = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "Single", TodoTimeKind.Single, new TimeOnly(9, 30), null));
        var range = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "Range", TodoTimeKind.Range, new TimeOnly(10, 0), new TimeOnly(11, 0)));

        Assert.Null(none.StartTime);
        Assert.Null(none.EndTime);
        Assert.Equal(new TimeOnly(9, 30), single.StartTime);
        Assert.Null(single.EndTime);
        Assert.Equal(new TimeOnly(10, 0), range.StartTime);
        Assert.Equal(new TimeOnly(11, 0), range.EndTime);
        Assert.Equal(3, repository.Todos.Count);
    }

    [Fact]
    public async Task CreateTodoAsync_RejectsInvalidTimeRange()
    {
        var service = new TodoService(new InMemoryTodoRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateTodoAsync(new TodoDraft(
                new DateOnly(2026, 7, 13),
                "Invalid",
                TodoTimeKind.Range,
                new TimeOnly(12, 0),
                new TimeOnly(11, 0))));
    }

    [Fact]
    public async Task SetCompletedAsync_MarksCompletedAndReopens()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository);
        var todo = await service.CreateTodoAsync(new TodoDraft(new DateOnly(2026, 7, 13), "Task", TodoTimeKind.None, null, null));

        await service.SetCompletedAsync(todo, true);
        Assert.True(todo.IsCompleted);
        Assert.NotNull(todo.CompletedAt);

        await service.SetCompletedAsync(todo, false);
        Assert.False(todo.IsCompleted);
        Assert.Null(todo.CompletedAt);
    }

    [Fact]
    public void SortTodos_PutsIncompleteTimedTodosFirstAndCompletedTodosLast()
    {
        var service = new TodoService(new InMemoryTodoRepository());
        var date = new DateOnly(2026, 7, 13);
        var todos = new[]
        {
            CreateTodo("done", date, "Done", true, null, null, new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc)),
            CreateTodo("untimed", date, "Untimed", false, null, null, null),
            CreateTodo("early", date, "Early", false, new TimeOnly(9, 0), null, null),
            CreateTodo("late", date, "Late", false, new TimeOnly(15, 0), null, null)
        };

        var sorted = service.SortTodos(todos);

        Assert.Collection(
            sorted,
            todo => Assert.Equal("early", todo.Id),
            todo => Assert.Equal("late", todo.Id),
            todo => Assert.Equal("untimed", todo.Id),
            todo => Assert.Equal("done", todo.Id));
    }

    private static TodoItem CreateTodo(
        string id,
        DateOnly date,
        string text,
        bool completed,
        TimeOnly? start,
        TimeOnly? end,
        DateTime? completedAt)
    {
        return new TodoItem
        {
            Id = id,
            Date = date,
            Text = text,
            IsCompleted = completed,
            StartTime = start,
            EndTime = end,
            CreatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            CompletedAt = completedAt
        };
    }

    private sealed class InMemoryTodoRepository : ITodoRepository
    {
        public List<TodoItem> Todos { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TodoItem>> GetActiveTodosAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TodoItem>>(
                Todos.Where(todo => !todo.IsDeleted && todo.Date >= fromDate && todo.Date <= toDate).ToList());
        }

        public Task AddAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            Todos.Add(todo);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(string todoId, DateTime deletedAt, CancellationToken cancellationToken = default)
        {
            var todo = Todos.Single(item => item.Id == todoId);
            todo.IsDeleted = true;
            todo.UpdatedAt = deletedAt;
            return Task.CompletedTask;
        }
    }
}
