using QingJian.App.Settings;

namespace QingJian.App.Tray;

public sealed class WindowBehaviorCoordinator
{
    public WindowBehaviorCoordinator(WindowBehaviorPreferences preferences)
    {
        CurrentPreferences = preferences ?? WindowBehaviorPreferences.Default;
    }

    public WindowBehaviorPreferences CurrentPreferences { get; private set; }

    public void Apply(WindowBehaviorPreferences preferences)
    {
        CurrentPreferences = preferences ?? WindowBehaviorPreferences.Default;
    }

    public bool ShouldHideOnMinimize(bool trayAvailable)
    {
        return trayAvailable && CurrentPreferences.MinimizeToTray;
    }

    public bool ShouldHideOnClose(bool explicitExit, bool trayAvailable)
    {
        return !explicitExit && trayAvailable && CurrentPreferences.CloseToTray;
    }

    public bool ShouldStartHidden(IReadOnlyList<string> args, bool trayAvailable)
    {
        return trayAvailable
            && CurrentPreferences.StartMinimized
            && args.Any(argument => string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase));
    }
}
