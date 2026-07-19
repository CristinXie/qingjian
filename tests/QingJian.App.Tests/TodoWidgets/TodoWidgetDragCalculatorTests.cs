using System.Windows;
using System.Windows.Media;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetDragCalculatorTests
{
    [Fact]
    public void CalculateWindowPosition_UsesDipDeltaOnScaledDisplays()
    {
        var transformFromDevice = new Matrix(2.0 / 3.0, 0, 0, 2.0 / 3.0, 0, 0);

        var position = TodoWidgetDragCalculator.CalculateWindowPosition(
            startLeft: 20,
            startTop: 40,
            startScreenPosition: new Point(100, 200),
            currentScreenPosition: new Point(130, 260),
            transformFromDevice);

        Assert.Equal(40, position.X);
        Assert.Equal(80, position.Y);
    }

    [Fact]
    public void CalculateWindowPosition_UsesPixelDeltaWhenTransformIsIdentity()
    {
        var position = TodoWidgetDragCalculator.CalculateWindowPosition(
            startLeft: 20,
            startTop: 40,
            startScreenPosition: new Point(100, 200),
            currentScreenPosition: new Point(130, 260),
            Matrix.Identity);

        Assert.Equal(50, position.X);
        Assert.Equal(100, position.Y);
    }
}
