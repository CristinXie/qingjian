using System.Windows;
using QingJian.App.Views;
using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class NoteDeletionConfirmationTests
{
    [Fact]
    public void BuildPrompt_IncludesTheNoteTitle()
    {
        Assert.Equal("确定要删除便签“测试标题”吗？", NoteDeletionConfirmation.BuildPrompt("测试标题"));
    }

    [Theory]
    [InlineData(MessageBoxResult.Yes, true)]
    [InlineData(MessageBoxResult.No, false)]
    public void IsConfirmed_OnlyAcceptsYes(MessageBoxResult result, bool expected)
    {
        Assert.Equal(expected, NoteDeletionConfirmation.IsConfirmed(result));
    }
}
