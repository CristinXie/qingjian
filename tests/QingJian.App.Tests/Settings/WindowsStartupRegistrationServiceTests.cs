using QingJian.App.Settings;
using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class WindowsStartupRegistrationServiceTests
{
    [Fact]
    public void SetEnabled_WritesQuotedExecutableForCurrentUser()
    {
        var registry = new FakeStartupRegistry();
        var service = new WindowsStartupRegistrationService(
            @"C:\Program Files\QingJian\QingJian.App.exe",
            registry);

        service.SetEnabled(true);

        Assert.Equal("QingJian", registry.LastSetName);
        Assert.Equal("\"C:\\Program Files\\QingJian\\QingJian.App.exe\"", registry.LastSetValue);
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void SetEnabledFalse_RemovesStartupValue()
    {
        var registry = new FakeStartupRegistry { Value = "old command" };
        var service = new WindowsStartupRegistrationService(@"C:\QingJian.exe", registry);

        service.SetEnabled(false);

        Assert.Equal("QingJian", registry.LastDeletedName);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void IsEnabled_UsesRegistryAsSourceOfTruth()
    {
        var registry = new FakeStartupRegistry { Value = "some command" };
        var service = new WindowsStartupRegistrationService(@"C:\QingJian.exe", registry);

        Assert.True(service.IsEnabled);

        registry.Value = null;
        Assert.False(service.IsEnabled);
    }

    private sealed class FakeStartupRegistry : IStartupRegistry
    {
        public string? Value { get; set; }

        public string? LastSetName { get; private set; }

        public string? LastSetValue { get; private set; }

        public string? LastDeletedName { get; private set; }

        public string? GetValue(string name) => Value;

        public void SetValue(string name, string value)
        {
            LastSetName = name;
            LastSetValue = value;
            Value = value;
        }

        public void DeleteValue(string name)
        {
            LastDeletedName = name;
            Value = null;
        }
    }
}
