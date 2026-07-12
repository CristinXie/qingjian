using System.Reflection;
using System.Windows;
using QingJian.App.QuickNotes;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteWindowTests
{
    [Fact]
    public void DecideClose_AllowsConfirmedDirtyDraftCloseWithoutReentrantClose()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "DecideClose",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var decision = method!.Invoke(null, new object[] { false, false, false, true, true });
        var shouldCancel = (bool)decision!.GetType().GetProperty("ShouldCancel")!.GetValue(decision)!;
        var shouldAllowFutureClose = (bool)decision.GetType().GetProperty("ShouldAllowFutureClose")!.GetValue(decision)!;

        Assert.False(shouldCancel);
        Assert.True(shouldAllowFutureClose);
    }

    [Fact]
    public void DecideClose_CancelsDirtyDraftClose_WhenDiscardIsRejected()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "DecideClose",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var decision = method!.Invoke(null, new object[] { false, false, false, true, false });
        var shouldCancel = (bool)decision!.GetType().GetProperty("ShouldCancel")!.GetValue(decision)!;
        var shouldAllowFutureClose = (bool)decision.GetType().GetProperty("ShouldAllowFutureClose")!.GetValue(decision)!;

        Assert.True(shouldCancel);
        Assert.False(shouldAllowFutureClose);
    }

    [Fact]
    public void PixelsToDips_UsesProvidedMonitorScale()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "PixelsToDips",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = Assert.IsType<Point>(method.Invoke(null, new object[] { new Point(240, 216), 1.25d, 1.5d }));

        Assert.Equal(192, result.X);
        Assert.Equal(144, result.Y);
    }

    [Fact]
    public void CalculateWindowPlacement_UsesMonitorRelativePixelsWithoutShiftingAcrossDisplays()
    {
        var pointType = typeof(QuickNoteWindow).GetNestedType("POINT", BindingFlags.NonPublic);
        var placementType = typeof(QuickNoteWindow).GetNestedType("WindowPlacement", BindingFlags.NonPublic);
        var method = typeof(QuickNoteWindow).GetMethod(
            "CalculateWindowPlacement",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(pointType);
        Assert.NotNull(placementType);
        Assert.NotNull(method);

        var cursor = Activator.CreateInstance(pointType!);
        pointType!.GetField("X")!.SetValue(cursor, 2220);
        pointType.GetField("Y")!.SetValue(cursor, 300);

        var workingArea = new Rect(1280, 0, 1280, 720);
        var placement = method!.Invoke(null, new object[] { cursor!, workingArea, 1.5d, 1.5d, 420d, 320d });

        Assert.NotNull(placement);
        Assert.Equal(2232, (int)placementType!.GetProperty("Left")!.GetValue(placement)!);
        Assert.Equal(312, (int)placementType.GetProperty("Top")!.GetValue(placement)!);
    }
}
