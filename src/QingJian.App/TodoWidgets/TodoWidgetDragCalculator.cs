using System.Windows;
using System.Windows.Media;

namespace QingJian.App.TodoWidgets;

public static class TodoWidgetDragCalculator
{
    public static Point CalculateWindowPosition(
        double startLeft,
        double startTop,
        Point startScreenPosition,
        Point currentScreenPosition,
        Matrix transformFromDevice)
    {
        var pixelDelta = currentScreenPosition - startScreenPosition;
        var dipDelta = transformFromDevice.Transform(pixelDelta);

        return new Point(startLeft + dipDelta.X, startTop + dipDelta.Y);
    }
}
