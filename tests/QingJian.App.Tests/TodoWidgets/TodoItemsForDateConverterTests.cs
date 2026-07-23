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

    [Fact]
    public void Convert_AttributesOvernightTodoOnlyToItsStartDate()
    {
        var startDate = new DateOnly(2026, 7, 23);
        var overnight = new TodoItem
        {
            Date = startDate,
            Text = "夜间值班",
            StartTime = new TimeOnly(23, 0),
            EndTime = new TimeOnly(1, 0)
        };
        var converter = new TodoItemsForDateConverter();

        var startDateResult = Convert(converter, startDate, new[] { overnight });
        var endDateResult = Convert(converter, startDate.AddDays(1), new[] { overnight });

        Assert.Equal(new[] { overnight }, startDateResult);
        Assert.Empty(endDateResult);
    }

    private static IEnumerable<TodoItem> Convert(
        TodoItemsForDateConverter converter,
        DateOnly date,
        IEnumerable<TodoItem> todos)
    {
        var result = converter.Convert(
            new object[] { date, todos },
            typeof(IEnumerable<TodoItem>),
            null!,
            CultureInfo.InvariantCulture);

        return Assert.IsAssignableFrom<IEnumerable<TodoItem>>(result);
    }
}
