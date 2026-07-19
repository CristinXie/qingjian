using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetWindowMessageFilterTests
{
    [Fact]
    public void ShouldBlockMinimize_ReturnsTrueForSystemMinimizeWhenWidgetIsNotBeingHidden()
    {
        Assert.True(TodoWidgetWindowMessageFilter.ShouldBlockMinimize(
            TodoWidgetWindowMessageFilter.WmSysCommand,
            new IntPtr(TodoWidgetWindowMessageFilter.ScMinimize),
            isHidingFromButton: false));
    }

    [Fact]
    public void ShouldBlockMinimize_ReturnsTrueWhenMinimizeCommandIncludesLowBits()
    {
        Assert.True(TodoWidgetWindowMessageFilter.ShouldBlockMinimize(
            TodoWidgetWindowMessageFilter.WmSysCommand,
            new IntPtr(TodoWidgetWindowMessageFilter.ScMinimize | 0x0002),
            isHidingFromButton: false));
    }

    [Fact]
    public void ShouldBlockMinimize_ReturnsFalseWhenWidgetIsBeingHiddenByUser()
    {
        Assert.False(TodoWidgetWindowMessageFilter.ShouldBlockMinimize(
            TodoWidgetWindowMessageFilter.WmSysCommand,
            new IntPtr(TodoWidgetWindowMessageFilter.ScMinimize),
            isHidingFromButton: true));
    }

    [Fact]
    public void ShouldBlockMinimize_ReturnsFalseForOtherMessages()
    {
        Assert.False(TodoWidgetWindowMessageFilter.ShouldBlockMinimize(
            msg: 0x0005,
            new IntPtr(TodoWidgetWindowMessageFilter.ScMinimize),
            isHidingFromButton: false));
    }
}
