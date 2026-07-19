using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetManagePopoverToggleTests
{
    private static readonly DateOnly SelectedDate = new(2026, 7, 17);

    [Fact]
    public void ShouldClose_ReturnsTrueForVisiblePopoverPinnedToClickedDate()
    {
        Assert.True(TodoWidgetManagePopoverToggle.ShouldClose(true, SelectedDate, SelectedDate));
    }

    [Fact]
    public void ShouldClose_ReturnsFalseWhenPopoverIsHidden()
    {
        Assert.False(TodoWidgetManagePopoverToggle.ShouldClose(false, SelectedDate, SelectedDate));
    }

    [Fact]
    public void ShouldClose_ReturnsFalseForDifferentPinnedDate()
    {
        Assert.False(TodoWidgetManagePopoverToggle.ShouldClose(true, SelectedDate.AddDays(1), SelectedDate));
    }
}
