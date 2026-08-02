using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoTimeRangeRulesTests
{
    [Theory]
    [InlineData(9, 10, true, false)]
    [InlineData(23, 1, true, true)]
    [InlineData(8, 8, false, false)]
    public void ClassifiesTimeRanges(int startHour, int endHour, bool valid, bool overnight)
    {
        var start = new TimeOnly(startHour, 0);
        var end = new TimeOnly(endHour, 0);

        Assert.Equal(valid, TodoTimeRangeRules.IsValidRange(start, end));
        Assert.Equal(overnight, TodoTimeRangeRules.IsOvernight(start, end));
    }

    [Fact]
    public void OvernightRangeCanApproachButNotReachTwentyFourHours()
    {
        var start = new TimeOnly(0, 1);
        var end = new TimeOnly(0, 0);
        var duration = TimeSpan.FromDays(1) - start.ToTimeSpan() + end.ToTimeSpan();

        Assert.True(TodoTimeRangeRules.IsOvernight(start, end));
        Assert.Equal(new TimeSpan(23, 59, 0), duration);
        Assert.True(duration < TimeSpan.FromDays(1));
        Assert.False(TodoTimeRangeRules.IsValidRange(end, end));
    }
}
