using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetVisibilityActionTests
{
    [Theory]
    [InlineData(false, "显示桌面待办")]
    [InlineData(true, "隐藏桌面待办")]
    public void GetLabel_DescribesTheNextVisibilityAction(bool isVisible, string expected)
    {
        Assert.Equal(expected, TodoWidgetVisibilityAction.GetLabel(isVisible));
    }
}
