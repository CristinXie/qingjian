using System.Globalization;
using QingJian.App.Models;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoTimeDisplayConverterTests
{
    [Fact]
    public void Convert_ReturnsEmptyTextForNoTimeTodo()
    {
        var converter = new TodoTimeDisplayConverter();

        var text = converter.Convert(new TodoItem(), typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void Convert_ReturnsSingleTime()
    {
        var converter = new TodoTimeDisplayConverter();
        var todo = new TodoItem { StartTime = new TimeOnly(9, 30) };

        var text = converter.Convert(todo, typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal("09:30", text);
    }

    [Fact]
    public void Convert_ReturnsTimeRange()
    {
        var converter = new TodoTimeDisplayConverter();
        var todo = new TodoItem
        {
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 45)
        };

        var text = converter.Convert(todo, typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal("09:30-10:45", text);
    }

    [Fact]
    public void Convert_LabelsOvernightRangeEndAsNextDay()
    {
        var converter = new TodoTimeDisplayConverter();
        var todo = new TodoItem
        {
            StartTime = new TimeOnly(23, 0),
            EndTime = new TimeOnly(1, 0)
        };

        var text = converter.Convert(todo, typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal("23:00-次日 01:00", text);
    }
}
