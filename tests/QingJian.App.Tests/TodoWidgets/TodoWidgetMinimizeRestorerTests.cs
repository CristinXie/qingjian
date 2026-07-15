using System.Windows;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetMinimizeRestorerTests
{
    [Fact]
    public void ShouldRestore_ReturnsTrueForSystemMinimizedVisibleWidget()
    {
        Assert.True(TodoWidgetMinimizeRestorer.ShouldRestore(WindowState.Minimized, isHidingFromButton: false));
    }

    [Fact]
    public void ShouldRestore_ReturnsFalseWhenWidgetIsBeingHiddenByUser()
    {
        Assert.False(TodoWidgetMinimizeRestorer.ShouldRestore(WindowState.Minimized, isHidingFromButton: true));
    }

    [Fact]
    public void ShouldRestore_ReturnsFalseForNormalWindowState()
    {
        Assert.False(TodoWidgetMinimizeRestorer.ShouldRestore(WindowState.Normal, isHidingFromButton: false));
    }
}
