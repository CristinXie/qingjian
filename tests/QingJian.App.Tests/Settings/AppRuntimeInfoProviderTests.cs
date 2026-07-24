using QingJian.App.Settings;
using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class AppRuntimeInfoProviderTests
{
    [Fact]
    public void GetInfo_ReturnsProvidedVersions()
    {
        var provider = new AppRuntimeInfoProvider(
            () => "1.2.3",
            () => ".NET 8.0.0",
            () => "126.0.0");

        var info = provider.GetInfo();

        Assert.Equal("1.2.3", info.AppVersion);
        Assert.Equal(".NET 8.0.0", info.DotNetVersion);
        Assert.Equal("126.0.0", info.WebView2Version);
    }

    [Fact]
    public void GetInfo_UsesChineseFallbackWhenWebView2CannotBeRead()
    {
        var provider = new AppRuntimeInfoProvider(
            () => "1.0.0",
            () => ".NET 8",
            () => throw new InvalidOperationException());

        var info = provider.GetInfo();

        Assert.Equal("未检测到", info.WebView2Version);
    }
}
