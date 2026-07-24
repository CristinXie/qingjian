using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class SettingsWindowXamlTests
{
    [Fact]
    public void Window_HasStableDimensionsAndSixOrderedSections()
    {
        var xaml = XDocument.Load(FindSettingsWindowXamlPath());
        var window = xaml.Root ?? throw new InvalidOperationException("Settings window root is missing.");

        Assert.Equal("720", (string?)window.Attribute("Width"));
        Assert.Equal("560", (string?)window.Attribute("Height"));
        Assert.Equal("720", (string?)window.Attribute("MinWidth"));
        Assert.Equal("560", (string?)window.Attribute("MinHeight"));

        var headers = xaml.Descendants()
            .Where(element => element.Name.LocalName == "TabItem")
            .Select(element => (string?)element.Attribute("Header"))
            .ToArray();
        Assert.Equal(new[] { "常规", "编辑", "快捷便签", "桌面待办", "数据", "关于" }, headers);

        var tabControl = FindNamedElement(xaml, "SettingsTabControl");
        Assert.Equal("Stretch", (string?)tabControl.Attribute("HorizontalContentAlignment"));
        Assert.Equal("Stretch", (string?)tabControl.Attribute("VerticalContentAlignment"));

        var tabStyle = xaml.Descendants().Single(element =>
            element.Name.LocalName == "Style"
            && (string?)FindAttributeByLocalName(element, "Key") == "SettingsTabItemStyle");
        Assert.Contains(tabStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "HorizontalContentAlignment"
            && (string?)element.Attribute("Value") == "Stretch");
        Assert.Contains(tabStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "VerticalContentAlignment"
            && (string?)element.Attribute("Value") == "Stretch");
        Assert.DoesNotContain(tabStyle.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "FontWeight");
    }

    [Fact]
    public void Window_ContainsAllFunctionalControls()
    {
        var xaml = XDocument.Load(FindSettingsWindowXamlPath());
        var expected = new Dictionary<string, string>
        {
            ["LaunchAtStartupCheckBox"] = "CheckBox",
            ["WysiwygModeRadioButton"] = "RadioButton",
            ["MarkdownModeRadioButton"] = "RadioButton",
            ["HotkeyEnabledCheckBox"] = "CheckBox",
            ["HotkeyTextBox"] = "TextBox",
            ["EightDayModeRadioButton"] = "RadioButton",
            ["TodayModeRadioButton"] = "RadioButton",
            ["CalendarModeRadioButton"] = "RadioButton",
            ["TodoOpacitySlider"] = "Slider",
            ["TodoLockedCheckBox"] = "CheckBox",
            ["ResetTodoAppearanceButton"] = "Button",
            ["DataFolderTextBox"] = "TextBox",
            ["OpenDataFolderButton"] = "Button",
            ["DatabaseSizeTextBlock"] = "TextBlock",
            ["AttachmentSizeTextBlock"] = "TextBlock",
            ["TotalSizeTextBlock"] = "TextBlock",
            ["AppVersionTextBlock"] = "TextBlock",
            ["DotNetVersionTextBlock"] = "TextBlock",
            ["WebView2VersionTextBlock"] = "TextBlock",
            ["CancelButton"] = "Button",
            ["SaveButton"] = "Button",
            ["SaveErrorTextBlock"] = "TextBlock"
        };

        foreach (var pair in expected)
        {
            var element = FindNamedElement(xaml, pair.Key);
            Assert.Equal(pair.Value, element.Name.LocalName);
        }
    }

    [Fact]
    public void CommandsAndIconActionsUseAgreedStylesAndSpacing()
    {
        var xaml = XDocument.Load(FindSettingsWindowXamlPath());
        var openFolder = FindNamedElement(xaml, "OpenDataFolderButton");
        var cancel = FindNamedElement(xaml, "CancelButton");
        var save = FindNamedElement(xaml, "SaveButton");

        Assert.Equal("{StaticResource IconButtonStyle}", (string?)openFolder.Attribute("Style"));
        Assert.Equal("打开数据目录", (string?)openFolder.Attribute("ToolTip"));
        Assert.Equal("0", (string?)FindAttributeByLocalName(openFolder, "ToolTipService.InitialShowDelay"));
        Assert.Equal("{StaticResource SecondaryButtonStyle}", (string?)cancel.Attribute("Style"));
        Assert.Equal("{StaticResource PrimaryButtonStyle}", (string?)save.Attribute("Style"));
        Assert.Equal("8,0,0,0", (string?)save.Attribute("Margin"));
        Assert.Equal("False", (string?)save.Attribute("IsEnabled"));
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

    private static string FindSettingsWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App", "Settings", "SettingsWindow.xaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate SettingsWindow.xaml.");
    }
}
