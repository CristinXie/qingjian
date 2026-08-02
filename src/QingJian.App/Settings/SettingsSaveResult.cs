namespace QingJian.App.Settings;

public sealed record SettingsSaveResult(bool Succeeded, string? ErrorMessage)
{
    public static SettingsSaveResult Success() => new(true, null);

    public static SettingsSaveResult Failure(string message) => new(false, message);
}
