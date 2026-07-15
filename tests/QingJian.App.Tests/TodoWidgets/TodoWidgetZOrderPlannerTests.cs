using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetZOrderPlannerTests
{
    [Fact]
    public void GetInsertAfterHandle_ReturnsWindowAboveDesktopWhenWidgetIsBelowDesktop()
    {
        var normalWindow = new IntPtr(10);
        var desktopWindow = new IntPtr(20);
        var widgetWindow = new IntPtr(30);

        var plan = TodoWidgetZOrderPlanner.CreatePlan(
            widgetWindow,
            desktopWindow: desktopWindow,
            new[] { normalWindow, desktopWindow, widgetWindow });

        Assert.True(plan.ShouldMove);
        Assert.False(plan.ShouldFallbackToBottom);
        Assert.Equal(normalWindow, plan.InsertAfterHandle);
    }

    [Fact]
    public void GetInsertAfterHandle_ReturnsNullWhenWidgetIsAlreadyAboveDesktop()
    {
        var normalWindow = new IntPtr(10);
        var widgetWindow = new IntPtr(20);
        var desktopWindow = new IntPtr(30);

        var plan = TodoWidgetZOrderPlanner.CreatePlan(
            widgetWindow,
            desktopWindow: desktopWindow,
            new[] { normalWindow, widgetWindow, desktopWindow });

        Assert.False(plan.ShouldMove);
        Assert.False(plan.ShouldFallbackToBottom);
        Assert.Null(plan.InsertAfterHandle);
    }

    [Fact]
    public void GetInsertAfterHandle_ReturnsTopWhenDesktopIsTopmostWindow()
    {
        var desktopWindow = new IntPtr(10);
        var widgetWindow = new IntPtr(20);

        var plan = TodoWidgetZOrderPlanner.CreatePlan(
            widgetWindow,
            desktopWindow: desktopWindow,
            new[] { desktopWindow, widgetWindow });

        Assert.True(plan.ShouldMove);
        Assert.Equal(TodoWidgetZOrderPlanner.HwndTop, plan.InsertAfterHandle);
    }

    [Fact]
    public void CreatePlan_FallsBackToBottomWhenDesktopCannotBeFound()
    {
        var widgetWindow = new IntPtr(20);

        var plan = TodoWidgetZOrderPlanner.CreatePlan(
            widgetWindow,
            desktopWindow: null,
            new[] { widgetWindow });

        Assert.True(plan.ShouldMove);
        Assert.True(plan.ShouldFallbackToBottom);
        Assert.Equal(TodoWidgetZOrderPlanner.HwndBottom, plan.InsertAfterHandle);
    }
}
