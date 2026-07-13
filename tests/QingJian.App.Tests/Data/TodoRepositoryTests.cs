using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Models;
using Xunit;

namespace QingJian.App.Tests.Data;

public sealed class TodoRepositoryTests
{
    [Fact]
    public async Task InitializeAsync_CreatesTodoItemsTableAndPersistsTodo()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);

        await repository.InitializeAsync();
        await repository.AddAsync(CreateTodo("todo-1", new DateOnly(2026, 7, 13), "Write plan"));

        var todos = await repository.GetActiveTodosAsync(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 13));

        Assert.Single(todos);
        Assert.Equal("todo-1", todos[0].Id);
        Assert.Equal("Write plan", todos[0].Text);
        Assert.Equal(new DateOnly(2026, 7, 13), todos[0].Date);
    }

    [Fact]
    public async Task GetActiveTodosAsync_ReturnsDateRangeAndExcludesDeleted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        await repository.AddAsync(CreateTodo("before", new DateOnly(2026, 7, 12), "Before"));
        await repository.AddAsync(CreateTodo("inside", new DateOnly(2026, 7, 13), "Inside"));
        await repository.AddAsync(CreateTodo("after", new DateOnly(2026, 7, 20), "After"));
        await repository.SoftDeleteAsync("inside", new DateTime(2026, 7, 13, 9, 0, 0, DateTimeKind.Utc));
        await repository.AddAsync(CreateTodo("visible", new DateOnly(2026, 7, 14), "Visible"));

        var todos = await repository.GetActiveTodosAsync(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 14));

        Assert.Collection(todos, todo => Assert.Equal("visible", todo.Id));
    }

    [Fact]
    public async Task UpdateAsync_PersistsTodoFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = CreateRepository(connection);
        await repository.InitializeAsync();

        var todo = CreateTodo("todo-1", new DateOnly(2026, 7, 13), "Original");
        await repository.AddAsync(todo);

        todo.Text = "Changed";
        todo.StartTime = new TimeOnly(10, 30);
        todo.EndTime = new TimeOnly(11, 15);
        todo.IsCompleted = true;
        todo.CompletedAt = new DateTime(2026, 7, 13, 11, 15, 0, DateTimeKind.Utc);
        await repository.UpdateAsync(todo);

        var todos = await repository.GetActiveTodosAsync(
            new DateOnly(2026, 7, 13),
            new DateOnly(2026, 7, 13));

        Assert.Equal("Changed", todos[0].Text);
        Assert.Equal(new TimeOnly(10, 30), todos[0].StartTime);
        Assert.Equal(new TimeOnly(11, 15), todos[0].EndTime);
        Assert.True(todos[0].IsCompleted);
        Assert.NotNull(todos[0].CompletedAt);
    }

    private static TodoRepository CreateRepository(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new TodoRepository(new AppDbContext(options));
    }

    private static TodoItem CreateTodo(string id, DateOnly date, string text)
    {
        return new TodoItem
        {
            Id = id,
            Date = date,
            Text = text,
            CreatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            IsCompleted = false,
            IsDeleted = false
        };
    }
}
