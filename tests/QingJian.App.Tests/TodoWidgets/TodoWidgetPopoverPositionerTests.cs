using System.Windows;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetPopoverPositionerTests
{
    [Fact]
    public void CalculateNearAnchor_PlacesPopoverToRightWhenThereIsRoom()
    {
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            new Rect(40, 20, 20, 20),
            new Size(260, 220),
            new Size(520, 320),
            gap: 8);

        Assert.Equal(68, position.Left);
        Assert.Equal(20, position.Top);
    }

    [Fact]
    public void CalculateNearAnchor_PlacesPopoverToLeftWhenRightSideDoesNotFit()
    {
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            new Rect(480, 20, 20, 20),
            new Size(260, 220),
            new Size(520, 320),
            gap: 8);

        Assert.Equal(212, position.Left);
        Assert.Equal(20, position.Top);
    }

    [Fact]
    public void CalculateNearAnchor_ClampsPopoverInsideAvailableArea()
    {
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            new Rect(4, 310, 20, 20),
            new Size(260, 220),
            new Size(520, 320),
            gap: 8);

        Assert.Equal(32, position.Left);
        Assert.Equal(100, position.Top);
    }

    [Fact]
    public void CalculateNearAnchor_UsesClampedRightSideForSecondColumnToKeepAnchorClickable()
    {
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            new Rect(140, 20, 120, 100),
            new Size(260, 220),
            new Size(520, 320),
            gap: 8);

        Assert.Equal(260, position.Left);
    }

    [Fact]
    public void CalculateNearAnchor_UsesClampedLeftSideForThirdColumnToKeepAnchorClickable()
    {
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            new Rect(260, 20, 120, 100),
            new Size(260, 220),
            new Size(520, 320),
            gap: 8);

        Assert.Equal(0, position.Left);
    }

    [Theory]
    [InlineData(140, 182)]
    [InlineData(260, 0)]
    public void CalculateNearAnchor_KeepsMiddleColumnsPartlyClickableForWiderManagementPopover(
        double anchorLeft,
        double expectedLeft)
    {
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            new Rect(anchorLeft, 20, 120, 100),
            new Size(338, 280),
            new Size(520, 320),
            gap: 8);

        Assert.Equal(expectedLeft, position.Left);
    }
}
