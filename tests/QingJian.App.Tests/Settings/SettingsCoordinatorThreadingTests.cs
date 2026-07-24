using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class SettingsCoordinatorThreadingTests
{
    [Fact]
    public void UiCoordinator_PreservesDispatcherContextAcrossAwaitBoundaries()
    {
        var source = File.ReadAllText(FindSettingsCoordinatorPath());

        Assert.DoesNotContain("ConfigureAwait(false)", source, StringComparison.Ordinal);
    }

    private static string FindSettingsCoordinatorPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Settings",
                "SettingsCoordinator.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate SettingsCoordinator.cs.");
    }
}
