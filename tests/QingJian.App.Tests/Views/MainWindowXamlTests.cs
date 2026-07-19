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
        var deleteButton = FindNamedElement(xaml, "Button", "DeleteButton");

        Assert.NotEmpty(newNoteButtons);
        Assert.All(
            newNoteButtons,
            button => Assert.Equal("{StaticResource PrimaryButtonStyle}", (string?)button.Attribute("Style")));
        Assert.Equal("{StaticResource DangerIconButtonStyle}", (string?)deleteButton.Attribute("Style"));
    }

    [Fact]
    public void NoteList_UsesGroupedNavigationView()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var listBox = xaml.Descendants().Single(element => element.Name.LocalName == "ListBox");

        Assert.Equal("{Binding NotesView}", (string?)listBox.Attribute("ItemsSource"));
        Assert.Contains(
            listBox.Descendants(),
            element => element.Name.LocalName == "GroupStyle.HeaderTemplate");
    }

    [Fact]
    public void EditorActions_AppearInAgreedOrderWithSpacingAndImmediateTooltips()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var expectedNames = new[] { "ModeToggleButton", "UndoButton", "RedoButton", "DeleteButton" };
        var buttons = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => expectedNames.Contains((string?)FindAttributeByLocalName(element, "Name")))
            .ToArray();

        Assert.Equal(expectedNames, buttons.Select(button => (string?)FindAttributeByLocalName(button, "Name")));
        Assert.Null(buttons[0].Attribute("Margin"));
        Assert.All(buttons.Skip(1), button => Assert.Equal("8,0,0,0", (string?)button.Attribute("Margin")));
        Assert.All(
            buttons,
            button => Assert.Equal("0", (string?)FindAttributeByLocalName(button, "ToolTipService.InitialShowDelay")));
        Assert.Equal("撤销", (string?)buttons[1].Attribute("ToolTip"));
        Assert.Equal("恢复", (string?)buttons[2].Attribute("ToolTip"));
        Assert.Equal("删除", (string?)buttons[3].Attribute("ToolTip"));
        Assert.Equal("DeleteButton_OnClick", (string?)buttons[3].Attribute("Click"));
        Assert.Null(buttons[3].Attribute("Command"));
    }

    [Fact]
    public void EditorHeaderAndFooter_ExposeTabFlowAndCreationDate()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var titleTextBox = FindNamedElement(xaml, "TextBox", "TitleTextBox");
        var createdAtTextBlock = FindNamedElement(xaml, "TextBlock", "CreatedAtTextBlock");

        Assert.Equal("TitleTextBox_OnPreviewKeyDown", (string?)titleTextBox.Attribute("PreviewKeyDown"));
        Assert.Contains("SelectedNote.CreatedAt", (string?)createdAtTextBlock.Attribute("Text"));
        Assert.Contains("NoteCreatedAtDisplayConverter", (string?)createdAtTextBlock.Attribute("Text"));
        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "NoteCreatedAtDisplayConverter"
                && (string?)FindAttributeByLocalName(element, "Key") == "NoteCreatedAtDisplayConverter");
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

    [Fact]
    public void SidebarActionsAndNoteItems_UseEightDipRoundedBorders()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var styles = XDocument.Load(FindStylesXamlPath());
        var roundedStyle = styles
            .Descendants()
            .Single(element => element.Name.LocalName == "Style"
                && (string?)FindAttributeByLocalName(element, "Key") == "RoundedButtonStyle");
        var noteItemStyle = styles
            .Descendants()
            .Single(element => element.Name.LocalName == "Style"
                && (string?)FindAttributeByLocalName(element, "Key") == "NoteListBoxItemStyle");
        var roundedButtonBorder = roundedStyle
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute("CornerRadius") == "8");
        var roundedNoteItemBorder = noteItemStyle
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute("CornerRadius") == "8");
        var sidebarButtons = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") is "新建便签" or "显示/隐藏桌面待办")
            .ToArray();

        Assert.NotEmpty(sidebarButtons);
        Assert.All(sidebarButtons, button =>
        {
            var style = (string?)button.Attribute("Style");
            Assert.Contains(style, new[]
            {
                "{StaticResource PrimaryButtonStyle}",
                "{StaticResource SecondaryButtonStyle}"
            });
        });
        Assert.NotNull(roundedButtonBorder);
        Assert.NotNull(roundedNoteItemBorder);
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

    private static XElement FindNamedElement(XDocument document, string elementName, string name)
    {
        return document.Descendants().Single(element =>
            element.Name.LocalName == elementName
            && (string?)FindAttributeByLocalName(element, "Name") == name);
    }
}
