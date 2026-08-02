namespace QingJian.App.Settings;

public sealed record WindowBehaviorPreferences(
    bool MinimizeToTray,
    bool CloseToTray,
    bool StartMinimized)
{
    public static WindowBehaviorPreferences Default { get; } = new(
        MinimizeToTray: true,
        CloseToTray: false,
        StartMinimized: false);
}
