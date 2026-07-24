namespace QingJian.App.Settings;

public interface IStartupRegistrationService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
