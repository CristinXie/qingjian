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

        var insertAfter = TodoWidgetZOrderPlanner.GetInsertAfterHandle(
            widgetWindow,
            desktopWindow,
            new[] { normalWindow, desktopWindow, widgetWindow });

        Assert.Equal(normalWindow, insertAfter);
    }

    [Fact]
    public void GetInsertAfterHandle_ReturnsNullWhenWidgetIsAlreadyAboveDesktop()
    {
        var normalWindow = new IntPtr(10);
        var widgetWindow = new IntPtr(20);
        var desktopWindow = new IntPtr(30);

        var insertAfter = TodoWidgetZOrderPlanner.GetInsertAfterHandle(
            widgetWindow,
            desktopWindow,
            new[] { normalWindow, widgetWindow, desktopWindow });

        Assert.Null(insertAfter);
    }

    [Fact]
    public void GetInsertAfterHandle_ReturnsTopWhenDesktopIsTopmostWindow()
    {
        var desktopWindow = new IntPtr(10);
        var widgetWindow = new IntPtr(20);

        var insertAfter = TodoWidgetZOrderPlanner.GetInsertAfterHandle(
            widgetWindow,
            desktopWindow,
            new[] { desktopWindow, widgetWindow });

        Assert.Equal(TodoWidgetZOrderPlanner.HwndTop, insertAfter);
    }
}
