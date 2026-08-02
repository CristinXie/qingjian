using QingJian.App.Settings;
using QingJian.App.Tray;
using Xunit;

namespace QingJian.App.Tests.Tray;

public sealed class WindowBehaviorCoordinatorTests
{
    [Fact]
    public void DefaultPreferences_HideOnMinimizeButNotClose()
    {
        var coordinator = new WindowBehaviorCoordinator(WindowBehaviorPreferences.Default);

        Assert.True(coordinator.ShouldHideOnMinimize(trayAvailable: true));
        Assert.False(coordinator.ShouldHideOnClose(explicitExit: false, trayAvailable: true));
    }

    [Fact]
    public void Apply_UpdatesCloseAndMinimizeDecisionsImmediately()
    {
        var coordinator = new WindowBehaviorCoordinator(WindowBehaviorPreferences.Default);
        coordinator.Apply(new WindowBehaviorPreferences(false, true, false));

        Assert.False(coordinator.ShouldHideOnMinimize(trayAvailable: true));
        Assert.True(coordinator.ShouldHideOnClose(explicitExit: false, trayAvailable: true));
    }

    [Fact]
    public void ExplicitExitAndUnavailableTray_NeverHideWindow()
    {
        var coordinator = new WindowBehaviorCoordinator(new WindowBehaviorPreferences(true, true, true));

        Assert.False(coordinator.ShouldHideOnClose(explicitExit: true, trayAvailable: true));
        Assert.False(coordinator.ShouldHideOnClose(explicitExit: false, trayAvailable: false));
        Assert.False(coordinator.ShouldHideOnMinimize(trayAvailable: false));
    }

    [Theory]
    [InlineData(true, true, true, new[] { "--startup" })]
    [InlineData(false, true, false, new[] { "--startup" })]
    [InlineData(false, false, true, new[] { "--startup" })]
    [InlineData(false, true, true, new[] { "--other" })]
    public void ShouldStartHidden_RequiresPreferenceTrayAndStartupArgument(
        bool expected,
        bool trayAvailable,
        bool startMinimized,
        string[] args)
    {
        var coordinator = new WindowBehaviorCoordinator(
            new WindowBehaviorPreferences(true, true, startMinimized));

        Assert.Equal(expected, coordinator.ShouldStartHidden(args, trayAvailable));
    }
}
