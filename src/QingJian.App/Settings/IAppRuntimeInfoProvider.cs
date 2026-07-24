namespace QingJian.App.Settings;

public interface IAppRuntimeInfoProvider
{
    AppRuntimeInfo GetInfo();
}

public sealed record AppRuntimeInfo(string AppVersion, string DotNetVersion, string WebView2Version);
