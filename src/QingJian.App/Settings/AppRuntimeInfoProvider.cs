using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace QingJian.App.Settings;

public sealed class AppRuntimeInfoProvider : IAppRuntimeInfoProvider
{
    private readonly Func<string> _appVersionProvider;
    private readonly Func<string> _dotNetVersionProvider;
    private readonly Func<string?> _webView2VersionProvider;

    public AppRuntimeInfoProvider()
        : this(GetAppVersion, () => RuntimeInformation.FrameworkDescription,
            () => CoreWebView2Environment.GetAvailableBrowserVersionString())
    {
    }

    public AppRuntimeInfoProvider(
        Func<string> appVersionProvider,
        Func<string> dotNetVersionProvider,
        Func<string?> webView2VersionProvider)
    {
        _appVersionProvider = appVersionProvider;
        _dotNetVersionProvider = dotNetVersionProvider;
        _webView2VersionProvider = webView2VersionProvider;
    }

    public AppRuntimeInfo GetInfo()
    {
        var webView2Version = "未检测到";
        try
        {
            webView2Version = _webView2VersionProvider() ?? "未检测到";
            if (string.IsNullOrWhiteSpace(webView2Version))
            {
                webView2Version = "未检测到";
            }
        }
        catch (Exception)
        {
        }

        return new AppRuntimeInfo(
            _appVersionProvider(),
            _dotNetVersionProvider(),
            webView2Version);
    }

    private static string GetAppVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "未知";
    }
}
