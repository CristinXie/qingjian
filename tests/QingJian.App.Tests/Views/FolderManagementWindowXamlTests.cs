using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class FolderManagementWindowXamlTests
{
    [Fact]
    public void Window_HasStableDialogDimensionsAndFolderListControls()
    {
        var xaml = XDocument.Load(FindPath("FolderManagementWindow.xaml"));
        var window = xaml.Root ?? throw new InvalidOperationException("Folder manager root is missing.");

        Assert.Equal("480", (string?)window.Attribute("Width"));
        Assert.Equal("520", (string?)window.Attribute("Height"));
        Assert.Equal("480", (string?)window.Attribute("MinWidth"));
        Assert.Equal("360", (string?)window.Attribute("MinHeight"));
        Assert.Equal("文件夹", (string?)window.Attribute("Title"));

        Assert.Equal("Button", FindNamedElement(xaml, "NewFolderButton").Name.LocalName);
        Assert.Equal("Button", FindNamedElement(xaml, "AllNotesButton").Name.LocalName);
        Assert.Equal("ItemsControl", FindNamedElement(xaml, "FolderItemsControl").Name.LocalName);
        Assert.Equal("TextBlock", FindNamedElement(xaml, "ErrorTextBlock").Name.LocalName);
    }

    [Fact]
    public void IconActions_UseTransparentStyleAndImmediateChineseTooltips()
    {
        var xaml = XDocument.Load(FindPath("FolderManagementWindow.xaml"));

        var newButton = FindNamedElement(xaml, "NewFolderButton");
        Assert.Equal("{StaticResource IconButtonStyle}", (string?)newButton.Attribute("Style"));
        Assert.Equal("新建文件夹", (string?)newButton.Attribute("ToolTip"));
        Assert.Equal("0", (string?)FindAttributeByLocalName(newButton, "ToolTipService.InitialShowDelay"));

        var allNotesButton = FindNamedElement(xaml, "AllNotesButton");
        Assert.Equal("显示全部便签", (string?)allNotesButton.Attribute("ToolTip"));
        Assert.Equal("0", (string?)FindAttributeByLocalName(allNotesButton, "ToolTipService.InitialShowDelay"));
    }

    [Fact]
    public void NewFolderEditorPanel_UsesCreatingStateTrigger()
    {
        var xaml = XDocument.Load(FindPath("FolderManagementWindow.xaml"));
        var panel = FindNamedElement(xaml, "NewFolderEditorPanel");

        Assert.Null(panel.Attribute("Visibility"));
        Assert.Contains(
            panel.Descendants().Where(element => element.Name.LocalName == "DataTrigger"),
            trigger => (string?)FindAttributeByLocalName(trigger, "Binding") == "{Binding IsCreating}" &&
                       (string?)FindAttributeByLocalName(trigger, "Value") == "True");
    }

    private static XElement FindNamedElement(XDocument xaml, string name)
    {
        return xaml.Descendants().Single(element =>
            (string?)FindAttributeByLocalName(element, "Name") == name);
    }

    private static XAttribute? FindAttributeByLocalName(XElement element, string localName)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName);
    }

    private static string FindPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App", "Views", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName}.");
    }
}
