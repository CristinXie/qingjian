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

    [Fact]
    public void PopoverCompleteCheckbox_BindsCompletionOneWay()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var popoverCompleteCheckBox = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "CheckBox"
                && (string?)element.Attribute("Click") == "CompleteTodoCheckBox_OnClick");

        var isCheckedBinding = (string?)popoverCompleteCheckBox.Attribute("IsChecked");

        Assert.Contains("Binding IsCompleted", isCheckedBinding, StringComparison.Ordinal);
        Assert.Contains("Mode=OneWay", isCheckedBinding, StringComparison.Ordinal);
    }

    [Fact]
    public void LockCheckbox_SavesPreferencesWhenChanged()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var lockCheckBox = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "CheckBox"
                && (string?)element.Attribute("Content") == "锁定");

        Assert.Equal("LockCheckBox_OnChanged", (string?)lockCheckBox.Attribute("Checked"));
        Assert.Equal("LockCheckBox_OnChanged", (string?)lockCheckBox.Attribute("Unchecked"));
    }

    [Fact]
    public void TodayMode_HasSharedPopoverEntryPoint()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var todayEditButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "编辑今日");

        Assert.Equal("OpenTodayPopoverButton_OnClick", (string?)todayEditButton.Attribute("Click"));
    }

    [Fact]
    public void CalendarMode_RendersTodoMarkersWithoutTodoText()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());

        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "•");
    }

    [Fact]
    public void DragHandle_UsesManualDragHandlersInsteadOfWindowDragMove()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var dragHandle = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "DockPanel"
                && (string?)element.Attribute("MouseLeftButtonDown") == "DragHandle_OnMouseLeftButtonDown");

        Assert.Equal("DragHandle_OnMouseMove", (string?)dragHandle.Attribute("MouseMove"));
        Assert.Equal("DragHandle_OnMouseLeftButtonUp", (string?)dragHandle.Attribute("MouseLeftButtonUp"));
        Assert.Equal("Transparent", (string?)dragHandle.Attribute("Background"));
        Assert.DoesNotContain("DragMove()", source, StringComparison.Ordinal);
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

    private static string FindTodoWidgetWindowCodeBehindPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "TodoWidgets",
                "TodoWidgetWindow.xaml.cs");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find TodoWidgetWindow.xaml.cs from test output directory.");
    }
}
