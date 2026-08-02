using System.Windows;
using QingJian.App.Views;
using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class BatchNoteDeletionConfirmationTests
{
    [Fact]
    public void BuildPrompt_IncludesCountAndRecoveryWarning()
    {
        Assert.Equal(
            "确定删除选中的 3 条便签吗？\n\n删除后暂时无法在应用内恢复。",
            BatchNoteDeletionConfirmation.BuildPrompt(3));
    }

    [Theory]
    [InlineData(MessageBoxResult.Yes, true)]
    [InlineData(MessageBoxResult.No, false)]
    [InlineData(MessageBoxResult.Cancel, false)]
    public void IsConfirmed_OnlyAcceptsYes(MessageBoxResult result, bool expected)
    {
        Assert.Equal(expected, BatchNoteDeletionConfirmation.IsConfirmed(result));
    }
}
