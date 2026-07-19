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
        Assert.Equal("{StaticResource SecondaryButtonStyle}", (string?)toggleButton.Attribute("Style"));
    }

    [Fact]
    public void NoteList_ShowsUpdatedTimeAsSecondaryMetadata()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());

        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && ((string?)element.Attribute("Text"))?.Contains("UpdatedAt", StringComparison.Ordinal) == true
                && (string?)element.Attribute("Foreground") == "{StaticResource MutedTextBrush}");
    }

    [Fact]
    public void NoteList_UsesDedicatedItemContainerStyleForSelectedAndHoverStates()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var listBox = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "ListBox"
                && ((string?)element.Attribute("ItemsSource"))?.Contains("Notes", StringComparison.Ordinal) == true);
        var styles = XDocument.Load(FindStylesXamlPath());

        Assert.Equal("{StaticResource NoteListBoxItemStyle}", (string?)listBox.Attribute("ItemContainerStyle"));
        Assert.Contains(
            styles.Descendants(),
            element => element.Name.LocalName == "Style"
                && (string?)FindAttributeByLocalName(element, "Key") == "NoteListBoxItemStyle");
    }

    [Fact]
    public void MainActions_UsePurposefulButtonStyles()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var newNoteButtons = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "新建便签")
            .ToArray();
        var deleteButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "删除");

        Assert.NotEmpty(newNoteButtons);
        Assert.All(
            newNoteButtons,
            button => Assert.Equal("{StaticResource PrimaryButtonStyle}", (string?)button.Attribute("Style")));
        Assert.Equal("{StaticResource SubtleDangerButtonStyle}", (string?)deleteButton.Attribute("Style"));
    }

    [Fact]
    public void Editor_IsWrappedInReadableShell()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var editorShell = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)FindAttributeByLocalName(element, "Name") == "EditorShell");

        Assert.Equal("{StaticResource EditorShellBackgroundBrush}", (string?)editorShell.Attribute("Background"));
        Assert.Equal("{StaticResource BorderBrush}", (string?)editorShell.Attribute("BorderBrush"));
        Assert.Equal("1", (string?)editorShell.Attribute("BorderThickness"));
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

    private static string FindStylesXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Resources",
                "Styles.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find Styles.xaml from test output directory.");
    }

    private static XAttribute? FindAttributeByLocalName(XElement element, string localName)
    {
        return element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName);
    }
}
