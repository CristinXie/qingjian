using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class RecycleBinWindowXamlTests
{
    [Fact]
    public void Window_ShowsThreeLineCardsAndImmediateLifecycleIconButtons()
    {
        var xaml = XDocument.Load(FindPath("RecycleBinWindow.xaml"));
        var items = FindNamedElement(xaml, "ItemsControl", "DeletedNotesItemsControl");
        var restore = FindNamedElement(xaml, "Button", "RestoreNoteButton");
        var delete = FindNamedElement(xaml, "Button", "PermanentlyDeleteNoteButton");
        var empty = FindNamedElement(xaml, "TextBlock", "RecycleBinEmptyTextBlock");

        Assert.Equal("回收站", (string?)xaml.Root?.Attribute("Title"));
        Assert.Equal("{Binding DeletedNotes}", (string?)items.Attribute("ItemsSource"));
        Assert.Contains(items.Descendants(), element =>
            element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding Title}");
        Assert.Contains(items.Descendants(), element =>
            element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("NoteUpdatedAtDisplayConverter", StringComparison.Ordinal) == true);
        Assert.Contains(items.Descendants(), element =>
            element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding FolderName}");
        Assert.Contains(items.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding IsFavorite}");
        var favoriteIcon = items.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock"
            && element.Descendants().Any(trigger =>
                trigger.Name.LocalName == "DataTrigger"
                && (string?)trigger.Attribute("Binding") == "{Binding IsFavorite}"));
        Assert.Null(favoriteIcon.Attribute("Text"));
        Assert.Contains(favoriteIcon.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "Text"
            && (string?)element.Attribute("Value") == "\uE734");
        Assert.Contains(favoriteIcon.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && element.Descendants().Any(setter =>
                setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "Text"
                && (string?)setter.Attribute("Value") == "\uE735"));
        Assert.Equal("{StaticResource IconButtonStyle}", (string?)restore.Attribute("Style"));
        Assert.Equal("恢复", (string?)restore.Attribute("ToolTip"));
        Assert.Equal("RestoreNoteButton_OnClick", (string?)restore.Attribute("Click"));
        Assert.Equal("{StaticResource DangerIconButtonStyle}", (string?)delete.Attribute("Style"));
        Assert.Equal("永久删除", (string?)delete.Attribute("ToolTip"));
        Assert.Equal("8,0,0,0", (string?)delete.Attribute("Margin"));
        Assert.Equal("PermanentlyDeleteNoteButton_OnClick", (string?)delete.Attribute("Click"));
        Assert.All(new[] { restore, delete }, button =>
            Assert.Equal("0", (string?)FindAttributeByLocalName(button, "ToolTipService.InitialShowDelay")));
        Assert.Equal("近30天没有被删除的便签", (string?)empty.Attribute("Text"));
    }

    private static XElement FindNamedElement(XDocument xaml, string localName, string name)
        => xaml.Descendants().Single(element =>
            element.Name.LocalName == localName
            && (string?)FindAttributeByLocalName(element, "Name") == name);

    private static XAttribute? FindAttributeByLocalName(XElement element, string localName)
        => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName);

    private static string FindPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "QingJian.App", "Views", fileName);
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {fileName}.");
    }
}
