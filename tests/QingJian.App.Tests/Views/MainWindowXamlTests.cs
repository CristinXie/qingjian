using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class MainWindowXamlTests
{
    [Fact]
    public void Sidebar_ContainsTodoWidgetToggleButton()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());

        var toggleButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "显示/隐藏桌面待办");

        Assert.Equal("ToggleTodoWidgetButton_OnClick", (string?)toggleButton.Attribute("Click"));
    }

    private static string FindMainWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Views",
                "MainWindow.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find MainWindow.xaml from test output directory.");
    }
}
