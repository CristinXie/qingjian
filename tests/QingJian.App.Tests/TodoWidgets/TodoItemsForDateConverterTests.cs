using System.Globalization;
using QingJian.App.Models;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoItemsForDateConverterTests
{
    [Fact]
    public void Convert_ReturnsOnlyTodosForDate()
    {
        var date = new DateOnly(2026, 7, 13);
        var matching = new TodoItem { Date = date, Text = "Matching" };
        var other = new TodoItem { Date = date.AddDays(1), Text = "Other" };
        var converter = new TodoItemsForDateConverter();

        var result = converter.Convert(
            new object[] { date, new[] { matching, other } },
            typeof(IEnumerable<TodoItem>),
            null!,
            CultureInfo.InvariantCulture);

        var todos = Assert.IsAssignableFrom<IEnumerable<TodoItem>>(result);
        Assert.Equal(new[] { matching }, todos);
    }

    [Fact]
    public void Convert_WithInvalidValues_ReturnsEmptyTodos()
    {
        var converter = new TodoItemsForDateConverter();

        var result = converter.Convert(
            new object[] { "not a date", Array.Empty<TodoItem>() },
            typeof(IEnumerable<TodoItem>),
            null!,
            CultureInfo.InvariantCulture);

        var todos = Assert.IsAssignableFrom<IEnumerable<TodoItem>>(result);
        Assert.Empty(todos);
    }
}
