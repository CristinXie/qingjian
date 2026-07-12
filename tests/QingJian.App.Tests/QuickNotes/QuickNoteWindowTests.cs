using System.Reflection;
using System.Windows;
using System.Xml.Linq;
using QingJian.App.QuickNotes;
using Xunit;

namespace QingJian.App.Tests.QuickNotes;

public sealed class QuickNoteWindowTests
{
    [Theory]
    [InlineData("", "0 行 0 字")]
    [InlineData("你好", "1 行 2 字")]
    [InlineData("你好\r\n世界", "2 行 4 字")]
    [InlineData("第一行\n", "2 行 3 字")]
    public void FormatBodyStats_CountsLinesAndCharactersWithoutNewlineCharacters(string text, string expected)
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "FormatBodyStats",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = Assert.IsType<string>(method!.Invoke(null, new object[] { text }));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Footer_ShowsBodyStatsInsteadOfShortcutHint()
    {
        var xaml = XDocument.Load(FindQuickNoteWindowXamlPath());

        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (string?)FindAttributeByLocalName(element, "Name") == "BodyStatsTextBlock"
                && (string?)element.Attribute("Text") == "0 行 0 字");
        Assert.DoesNotContain(
            xaml.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && ((string?)element.Attribute("Text"))?.Contains("Ctrl+Enter", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void DragHandle_ShowsThreeCenteredGripLines()
    {
        var xaml = XDocument.Load(FindQuickNoteWindowXamlPath());
        var dragHandle = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)FindAttributeByLocalName(element, "Name") == "DragHandle");
        var gripLines = dragHandle
            .Descendants()
            .Where(element => element.Name.LocalName == "Border"
                && ((string?)FindAttributeByLocalName(element, "Name"))?.StartsWith("DragHandleLine", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal("Center", (string?)dragHandle.Attribute("HorizontalAlignment"));
        Assert.Equal(3, gripLines.Count);
        Assert.All(gripLines, line =>
        {
            Assert.Equal("24", (string?)line.Attribute("Width"));
            Assert.Equal("1", (string?)line.Attribute("Height"));
        });
    }

    [Fact]
    public void QuickNoteBackground_IsPureWhite()
    {
        var styles = XDocument.Load(FindStylesXamlPath());
        var quickNoteBackground = FindSolidColorBrush(styles, "QuickNoteBackgroundBrush");

        Assert.Equal("#FFFFFF", (string?)quickNoteBackground.Attribute("Color"));
    }

    [Fact]
    public void QuickNoteBorder_UsesNeutralBorderColor()
    {
        var styles = XDocument.Load(FindStylesXamlPath());
        var quickNoteBorder = FindSolidColorBrush(styles, "QuickNoteBorderBrush");

        Assert.Equal("#DDD7CF", (string?)quickNoteBorder.Attribute("Color"));
    }

    [Fact]
    public void TitlePlaceholder_DoesNotSetLocalVisibilityThatOverridesStyleTrigger()
    {
        var xaml = XDocument.Load(FindQuickNoteWindowXamlPath());
        var placeholder = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock" && (string?)element.Attribute("Text") == "标题");

        Assert.Null(placeholder.Attribute("Visibility"));
    }

    [Fact]
    public void DecideClose_AllowsConfirmedDirtyDraftCloseWithoutReentrantClose()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "DecideClose",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var decision = method!.Invoke(null, new object[] { false, false, false, true, true });
        var shouldCancel = (bool)decision!.GetType().GetProperty("ShouldCancel")!.GetValue(decision)!;
        var shouldAllowFutureClose = (bool)decision.GetType().GetProperty("ShouldAllowFutureClose")!.GetValue(decision)!;

        Assert.False(shouldCancel);
        Assert.True(shouldAllowFutureClose);
    }

    [Fact]
    public void DecideClose_CancelsDirtyDraftClose_WhenDiscardIsRejected()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "DecideClose",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var decision = method!.Invoke(null, new object[] { false, false, false, true, false });
        var shouldCancel = (bool)decision!.GetType().GetProperty("ShouldCancel")!.GetValue(decision)!;
        var shouldAllowFutureClose = (bool)decision.GetType().GetProperty("ShouldAllowFutureClose")!.GetValue(decision)!;

        Assert.True(shouldCancel);
        Assert.False(shouldAllowFutureClose);
    }

    [Fact]
    public void PixelsToDips_UsesProvidedMonitorScale()
    {
        var method = typeof(QuickNoteWindow).GetMethod(
            "PixelsToDips",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = Assert.IsType<Point>(method.Invoke(null, new object[] { new Point(240, 216), 1.25d, 1.5d }));

        Assert.Equal(192, result.X);
        Assert.Equal(144, result.Y);
    }

    [Fact]
    public void CalculateWindowPlacement_UsesMonitorRelativePixelsWithoutShiftingAcrossDisplays()
    {
        var pointType = typeof(QuickNoteWindow).GetNestedType("POINT", BindingFlags.NonPublic);
        var placementType = typeof(QuickNoteWindow).GetNestedType("WindowPlacement", BindingFlags.NonPublic);
        var method = typeof(QuickNoteWindow).GetMethod(
            "CalculateWindowPlacement",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(pointType);
        Assert.NotNull(placementType);
        Assert.NotNull(method);

        var cursor = Activator.CreateInstance(pointType!);
        pointType!.GetField("X")!.SetValue(cursor, 2220);
        pointType.GetField("Y")!.SetValue(cursor, 300);

        var workingArea = new Rect(1280, 0, 1280, 720);
        var placement = method!.Invoke(null, new object[] { cursor!, workingArea, 1.5d, 1.5d, 420d, 320d });

        Assert.NotNull(placement);
        Assert.Equal(2232, (int)placementType!.GetProperty("Left")!.GetValue(placement)!);
        Assert.Equal(312, (int)placementType.GetProperty("Top")!.GetValue(placement)!);
    }

    private static string FindQuickNoteWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "QuickNotes",
                "QuickNoteWindow.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find QuickNoteWindow.xaml from test output directory.");
    }

    private static XAttribute? FindAttributeByLocalName(XElement element, string localName)
    {
        return element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName);
    }

    private static XElement FindSolidColorBrush(XDocument document, string key)
    {
        return document
            .Descendants()
            .Single(element => element.Name.LocalName == "SolidColorBrush"
                && (string?)FindAttributeByLocalName(element, "Key") == key);
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
}
