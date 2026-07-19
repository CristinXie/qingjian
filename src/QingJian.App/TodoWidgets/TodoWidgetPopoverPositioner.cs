using System.Windows;

namespace QingJian.App.TodoWidgets;

public static class TodoWidgetPopoverPositioner
{
    public static TodoWidgetPopoverPosition CalculateNearAnchor(
        Rect anchorBounds,
        Size popoverSize,
        Size availableSize,
        double gap)
    {
        var maxLeft = Math.Max(0, availableSize.Width - popoverSize.Width);
        var rightSideLeft = anchorBounds.Right + gap;
        var leftSideLeft = anchorBounds.Left - gap - popoverSize.Width;
        var rightCandidate = Clamp(rightSideLeft, 0, maxLeft);
        var leftCandidate = Clamp(leftSideLeft, 0, maxLeft);
        var rightOverlap = HorizontalOverlap(rightCandidate, popoverSize.Width, anchorBounds);
        var leftOverlap = HorizontalOverlap(leftCandidate, popoverSize.Width, anchorBounds);
        var preferredLeft = rightOverlap <= leftOverlap ? rightCandidate : leftCandidate;

        return new TodoWidgetPopoverPosition(
            preferredLeft,
            Clamp(anchorBounds.Top, 0, Math.Max(0, availableSize.Height - popoverSize.Height)));
    }

    private static double HorizontalOverlap(double popoverLeft, double popoverWidth, Rect anchorBounds)
    {
        var overlapLeft = Math.Max(popoverLeft, anchorBounds.Left);
        var overlapRight = Math.Min(popoverLeft + popoverWidth, anchorBounds.Right);
        return Math.Max(0, overlapRight - overlapLeft);
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(Math.Max(value, min), max);
    }
}
