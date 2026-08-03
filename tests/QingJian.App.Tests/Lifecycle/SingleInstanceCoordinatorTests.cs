using QingJian.App.Lifecycle;
using Xunit;

namespace QingJian.App.Tests.Lifecycle;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void FirstCoordinatorOwnsApplicationKeyAndSecondDoesNot()
    {
        var key = CreateUniqueKey();
        using var primary = SingleInstanceCoordinator.Create(key);
        using var secondary = SingleInstanceCoordinator.Create(key);

        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);
    }

    [Fact]
    public async Task SecondaryCoordinatorSignalsPrimaryListener()
    {
        var key = CreateUniqueKey();
        using var primary = SingleInstanceCoordinator.Create(key);
        using var secondary = SingleInstanceCoordinator.Create(key);
        var activated = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        primary.StartListening(() => activated.TrySetResult());

        Assert.True(secondary.NotifyPrimary());
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DisposingAllCoordinatorsReleasesApplicationKey()
    {
        var key = CreateUniqueKey();
        var primary = SingleInstanceCoordinator.Create(key);
        var secondary = SingleInstanceCoordinator.Create(key);

        secondary.Dispose();
        primary.Dispose();

        using var replacement = SingleInstanceCoordinator.Create(key);
        Assert.True(replacement.IsPrimary);
    }

    private static string CreateUniqueKey()
    {
        return $"QingJian.Tests.{Guid.NewGuid():N}";
    }
}
