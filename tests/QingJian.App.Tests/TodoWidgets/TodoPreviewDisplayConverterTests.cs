using System.Globalization;
using QingJian.App.Models;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoPreviewDisplayConverterTests
{
    private readonly TodoPreviewDisplayConverter _converter = new();

    [Fact]
    public void Convert_ReturnsOnlyContentForNoTimeTodo()
    {
        var todo = new TodoItem { Text = "整理资料" };

        Assert.Equal("整理资料", Convert(todo));
    }

    [Fact]
    public void Convert_PrefixesSingleTime()
    {
        var todo = new TodoItem { Text = "开会", StartTime = new TimeOnly(9, 30) };

        Assert.Equal("09:30 开会", Convert(todo));
    }

    [Fact]
    public void Convert_PrefixesTimeRange()
    {
        var todo = new TodoItem
        {
            Text = "专注工作",
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 45)
        };

        Assert.Equal("09:30-10:45 专注工作", Convert(todo));
    }

    private string Convert(TodoItem todo)
    {
        return (string)_converter.Convert(todo, typeof(string), null!, CultureInfo.InvariantCulture);
    }
}
