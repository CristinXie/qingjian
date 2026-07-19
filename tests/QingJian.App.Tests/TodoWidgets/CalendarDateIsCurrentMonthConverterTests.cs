using System.Globalization;
using System.Windows.Data;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class CalendarDateIsCurrentMonthConverterTests
{
    [Theory]
    [InlineData(2026, 7, 31, true)]
    [InlineData(2026, 6, 30, false)]
    [InlineData(2026, 8, 1, false)]
    public void Convert_ReturnsWhetherDateBelongsToDisplayedMonth(
        int year,
        int month,
        int day,
        bool expected)
    {
        var converterType = typeof(TodoWidgetViewModel).Assembly.GetType(
            "QingJian.App.TodoWidgets.CalendarDateIsCurrentMonthConverter");
        Assert.NotNull(converterType);
        var converter = Assert.IsAssignableFrom<IMultiValueConverter>(Activator.CreateInstance(converterType));

        var result = converter.Convert(
            new object[] { new DateOnly(year, month, day), new DateOnly(2026, 7, 1) },
            typeof(bool),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Equal(expected, Assert.IsType<bool>(result));
    }
}
