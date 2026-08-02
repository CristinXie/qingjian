namespace QingJian.App.Settings;

public sealed class WindowsStartupRegistrationService : IStartupRegistrationService
{
    public const string StartupValueName = "QingJian";

    private readonly string _startupCommand;
    private readonly IStartupRegistry _registry;

    public WindowsStartupRegistrationService(string executablePath)
        : this(executablePath, new CurrentUserStartupRegistry())
    {
    }

    public WindowsStartupRegistrationService(string executablePath, IStartupRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _startupCommand = $"\"{executablePath.Trim('"')}\" --startup";
        _registry = registry;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_registry.GetValue(StartupValueName));

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            _registry.SetValue(StartupValueName, _startupCommand);
            return;
        }

        _registry.DeleteValue(StartupValueName);
    }
}
