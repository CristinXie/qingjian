using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetWindowXamlTests
{
    [Fact]
    public void QuickCompleteCheckboxes_BindCompletionOneWay()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var quickCompleteCheckboxes = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "CheckBox"
                && (string?)element.Attribute("Click") == "QuickCompleteTodoCheckBox_OnClick")
            .ToList();

        Assert.Equal(2, quickCompleteCheckboxes.Count);
        Assert.All(quickCompleteCheckboxes, checkBox =>
        {
            var isCheckedBinding = (string?)checkBox.Attribute("IsChecked");

            Assert.Contains("Binding IsCompleted", isCheckedBinding, StringComparison.Ordinal);
            Assert.Contains("Mode=OneWay", isCheckedBinding, StringComparison.Ordinal);
        });
    }

    private static string FindTodoWidgetWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "TodoWidgets",
                "TodoWidgetWindow.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find TodoWidgetWindow.xaml from test output directory.");
    }
}
