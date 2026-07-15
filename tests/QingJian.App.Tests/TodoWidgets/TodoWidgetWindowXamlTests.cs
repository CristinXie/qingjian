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
    public void PopoverTodoTextInput_HasVisibleContentField()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var todoTextBox = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBox"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodoTextBox");

        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "待办内容");
        Assert.Equal("1", (string?)todoTextBox.Attribute("BorderThickness"));
        Assert.Equal("#FFFFFF", (string?)todoTextBox.Attribute("Background"));
        Assert.Equal("6,4", (string?)todoTextBox.Attribute("Padding"));
        Assert.Equal("64", (string?)todoTextBox.Attribute("MinHeight"));
    }

    [Fact]
    public void HideButton_IsOnlyVisibleWhenWidgetIsUnlocked()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var hideButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "隐藏");
        var lockTrigger = hideButton
            .Descendants()
            .Single(element => element.Name.LocalName == "DataTrigger"
                && (string?)element.Attribute("Binding") == "{Binding IsLocked}"
                && (string?)element.Attribute("Value") == "True");
        var visibilitySetter = lockTrigger
            .Descendants()
            .Single(element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "Visibility");

        Assert.Equal("HideButton", (string?)hideButton.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.Equal("Collapsed", (string?)visibilitySetter.Attribute("Value"));
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

    [Fact]
    public void Window_RestoresWhenSystemMinimizedByShowDesktop()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var window = xaml.Root ?? throw new InvalidOperationException("Todo widget XAML has no root element.");

        Assert.Equal("Window_OnStateChanged", (string?)window.Attribute("StateChanged"));
        Assert.Contains("Window_OnStateChanged", source, StringComparison.Ordinal);
        Assert.Contains("TodoWidgetMinimizeRestorer.ShouldRestore", source, StringComparison.Ordinal);
        Assert.Contains("MoveBehindOtherWindows();", source, StringComparison.Ordinal);
        Assert.Contains("DispatcherTimer", source, StringComparison.Ordinal);
        Assert.Contains("MinimizeRecoveryTimer_OnTick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (IsVisible && !_isHidingFromButton)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_BlocksSystemMinimizeMessagesFromShowDesktop()
    {
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());

        Assert.Contains("AddHook(WndProc)", source, StringComparison.Ordinal);
        Assert.Contains("TodoWidgetWindowMessageFilter.ShouldBlockMinimize", source, StringComparison.Ordinal);
        Assert.Contains("handled = true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_AttachesToDesktopOwnerWithoutBecomingDesktopChild()
    {
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var zOrderSource = File.ReadAllText(FindSourcePath("TodoWidgets", "WindowZOrderService.cs"));

        Assert.Contains("AttachToDesktopOwner(this)", source, StringComparison.Ordinal);
        Assert.Contains("GwlpHwndParent", zOrderSource, StringComparison.Ordinal);
        Assert.Contains("SetWindowLong", zOrderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetParent", zOrderSource, StringComparison.Ordinal);
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

    private static string FindSourcePath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName, "src", "QingJian.App" }.Concat(pathParts).ToArray());

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(pathParts)} from test output directory.");
    }
}
