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

        Assert.Equal(3, quickCompleteCheckboxes.Count);
        Assert.All(quickCompleteCheckboxes, checkBox =>
        {
            var isCheckedBinding = (string?)checkBox.Attribute("IsChecked");

            Assert.Contains("Binding IsCompleted", isCheckedBinding, StringComparison.Ordinal);
            Assert.Contains("Mode=OneWay", isCheckedBinding, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PopoverTodoList_DoesNotToggleCompletionFromPreviewOrManageRows()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var popoverCompleteCheckBoxes = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "CheckBox"
                && (string?)element.Attribute("Click") == "CompleteTodoCheckBox_OnClick")
            .ToList();

        Assert.Empty(popoverCompleteCheckBoxes);
    }

    [Fact]
    public void Toolbar_UsesModeCycleAndDynamicLockIconButtons()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var modeButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ModeSwitchButton");
        var lockButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "LockButton");
        var toolbarButtons = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Grid"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodoWidgetToolbarButtons");
        var toolbarColumns = toolbarButtons
            .Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .Select(element => (string?)element.Attribute("Width"))
            .ToList();
        var modeIcon = modeButton.Descendants().Single(element => element.Name.LocalName == "TextBlock");
        var lockIcon = lockButton.Descendants().Single(element => element.Name.LocalName == "TextBlock");
        var lockedTrigger = lockIcon
            .Descendants()
            .Single(element => element.Name.LocalName == "DataTrigger"
                && (string?)element.Attribute("Binding") == "{Binding IsLocked}"
                && (string?)element.Attribute("Value") == "True");

        Assert.Equal("CycleModeButton_OnClick", (string?)modeButton.Attribute("Click"));
        Assert.Equal(new[] { "Auto", "28", "Auto", "28", "Auto" }, toolbarColumns);
        Assert.Equal("0", (string?)modeButton.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)lockButton.Attribute("Grid.Column"));
        Assert.Equal("{Binding ModeSwitchToolTip}", (string?)modeButton.Attribute("ToolTip"));
        Assert.Equal("1000", (string?)modeButton.Attribute("ToolTipService.InitialShowDelay"));
        Assert.Equal("Segoe MDL2 Assets", (string?)modeIcon.Attribute("FontFamily"));
        Assert.Equal("LockButton_OnClick", (string?)lockButton.Attribute("Click"));
        Assert.Equal("{Binding LockActionToolTip}", (string?)lockButton.Attribute("ToolTip"));
        Assert.Equal("Segoe MDL2 Assets", (string?)lockIcon.Attribute("FontFamily"));
        Assert.Contains(
            lockedTrigger.Descendants(),
            element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "Text");
        Assert.DoesNotContain(
            xaml.Descendants(),
            element => element.Name.LocalName == "Button"
                && new[] { "8日", "今日", "日历" }.Contains((string?)element.Attribute("Content")));
        Assert.DoesNotContain(
            xaml.Descendants(),
            element => element.Name.LocalName == "CheckBox"
                && (string?)element.Attribute("Content") == "锁定");
        Assert.Contains("_viewModel.CycleMode();", source, StringComparison.Ordinal);
        Assert.Contains("_viewModel.IsLocked = !_viewModel.IsLocked;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TodayMode_HasCenteredQuickAddEntryPoint()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var quickAddButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodayQuickAddButton");

        Assert.Equal("+", (string?)quickAddButton.Attribute("Content"));
        Assert.Equal("Center", (string?)quickAddButton.Attribute("HorizontalAlignment"));
        Assert.Equal("AddTodayTodoButton_OnClick", (string?)quickAddButton.Attribute("Click"));
        Assert.DoesNotContain(
            xaml.Descendants(),
            element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "编辑今日");
        Assert.Contains("_viewModel.PinToday();", source, StringComparison.Ordinal);
        Assert.Contains("ShowQuickAddPopover(anchor);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TodayTodoRows_UseFixedRegionsWrappingContentAndVerticalScrolling()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var listBox = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "ListBox"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodayTodoListBox");
        var row = listBox
            .Descendants()
            .Single(element => element.Name.LocalName == "Grid"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodayTodoRowGrid");
        var columns = row
            .Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .Select(element => (string?)element.Attribute("Width"))
            .ToList();
        var checkBox = row.Descendants().Single(element => element.Name.LocalName == "CheckBox");
        var contentText = row
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Text}");
        var actionPanel = row
            .Descendants()
            .Single(element => element.Name.LocalName == "StackPanel"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodayTodoActionPanel");
        var editButton = actionPanel
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("ToolTip") == "修改");
        var deleteButton = actionPanel
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("ToolTip") == "删除");

        Assert.Equal(new[] { "80", "12", "Auto", "12", "*", "12", "52" }, columns);
        Assert.Equal("Disabled", (string?)listBox.Attribute("ScrollViewer.HorizontalScrollBarVisibility"));
        Assert.Equal("Auto", (string?)listBox.Attribute("ScrollViewer.VerticalScrollBarVisibility"));
        Assert.Equal("Stretch", (string?)listBox.Attribute("HorizontalContentAlignment"));
        Assert.Equal("2", (string?)checkBox.Attribute("Grid.Column"));
        Assert.Equal("QuickCompleteTodoCheckBox_OnClick", (string?)checkBox.Attribute("Click"));
        Assert.Equal("4", (string?)contentText.Attribute("Grid.Column"));
        Assert.Equal("Wrap", (string?)contentText.Attribute("TextWrapping"));
        Assert.Contains(
            contentText.Descendants(),
            element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "TextDecorations"
                && (string?)element.Attribute("Value") == "Strikethrough");
        Assert.Equal("6", (string?)actionPanel.Attribute("Grid.Column"));
        Assert.Equal("EditTodayTodoButton_OnClick", (string?)editButton.Attribute("Click"));
        Assert.Equal("DeleteTodoButton_OnClick", (string?)deleteButton.Attribute("Click"));
        Assert.Contains("BeginEditingTodo(todo);", source, StringComparison.Ordinal);
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
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "HideButton");
        var hideIcon = hideButton.Descendants().Single(element => element.Name.LocalName == "TextBlock");
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
        Assert.Equal("4", (string?)hideButton.Attribute("Grid.Column"));
        Assert.Equal("隐藏", (string?)hideButton.Attribute("ToolTip"));
        Assert.Equal("Segoe MDL2 Assets", (string?)hideIcon.Attribute("FontFamily"));
        Assert.Equal("HideButton_OnClick", (string?)hideButton.Attribute("Click"));
        Assert.Equal("Collapsed", (string?)visibilitySetter.Attribute("Value"));
    }

    [Fact]
    public void EightDayCells_HaveQuickAddButtonForTheDate()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var quickAddButton = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EightDayQuickAddButton");

        Assert.Equal("+", (string?)quickAddButton.Attribute("Content"));
        Assert.Equal("AddTodoForDateButton_OnClick", (string?)quickAddButton.Attribute("Click"));
        Assert.Equal("{Binding}", (string?)quickAddButton.Attribute("Tag"));
        Assert.Equal("Right", (string?)quickAddButton.Attribute("HorizontalAlignment"));
        Assert.Equal("Top", (string?)quickAddButton.Attribute("VerticalAlignment"));
        Assert.Contains("e.Handled = true;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddPopover_UsesTextAndSeparateHourMinuteInputs()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var quickAddPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddPopover");
        var quickAddTextBox = quickAddPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBox"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddTextBox");
        var timeComboBoxes = quickAddPopover
            .Descendants()
            .Where(element => element.Name.LocalName == "ComboBox")
            .ToList();
        var timeTextBoxes = quickAddPopover
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBox"
                && ((string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")))?.StartsWith("QuickAdd", StringComparison.Ordinal) == true
                && ((string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))) != "QuickAddTextBox")
            .ToList();
        var closeButton = quickAddPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "关闭");
        var createButton = quickAddPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "创建");

        Assert.Equal("1", (string?)quickAddTextBox.Attribute("BorderThickness"));
        Assert.Empty(timeComboBoxes);
        Assert.Equal(4, timeTextBoxes.Count);
        Assert.Contains(timeTextBoxes, textBox =>
            (string?)textBox.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddStartHourTextBox");
        Assert.Contains(timeTextBoxes, textBox =>
            (string?)textBox.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddStartMinuteTextBox");
        Assert.Contains(timeTextBoxes, textBox =>
            (string?)textBox.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddEndHourTextBox");
        Assert.Contains(timeTextBoxes, textBox =>
            (string?)textBox.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddEndMinuteTextBox");
        Assert.All(timeTextBoxes, textBox =>
        {
            Assert.Equal("2", (string?)textBox.Attribute("MaxLength"));
            Assert.Equal("Center", (string?)textBox.Attribute("TextAlignment"));
        });
        Assert.Equal("CloseQuickAddPopoverButton_OnClick", (string?)closeButton.Attribute("Click"));
        Assert.Equal("CreateQuickAddTodoButton_OnClick", (string?)createButton.Attribute("Click"));
        Assert.Contains("HideQuickAddPopoverAfterCreate();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EightDayDateCells_OpenManagementPopoverOnlyOnClick()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var dateCell = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EightDayDateCell");

        Assert.Equal("DateCell_OnMouseLeftButtonDown", (string?)dateCell.Attribute("MouseLeftButtonDown"));
        Assert.Null(dateCell.Attribute("MouseEnter"));
        Assert.Null(dateCell.Attribute("MouseLeave"));
        Assert.Contains("ShowTodoManagePopover(anchor, showEditor: false);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarDateCells_HaveQuickAddAndDelayedInteractivePreview()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var dateCell = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CalendarDateCell");
        var quickAddButton = dateCell
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CalendarQuickAddButton");

        Assert.Equal("CalendarDateCell_OnMouseEnter", (string?)dateCell.Attribute("MouseEnter"));
        Assert.Equal("CalendarDateCell_OnMouseLeave", (string?)dateCell.Attribute("MouseLeave"));
        Assert.Equal("+", (string?)quickAddButton.Attribute("Content"));
        Assert.Equal("{Binding}", (string?)quickAddButton.Attribute("Tag"));
        Assert.Equal("AddTodoForDateButton_OnClick", (string?)quickAddButton.Attribute("Click"));
        Assert.Equal("Right", (string?)quickAddButton.Attribute("HorizontalAlignment"));
        Assert.Equal("Top", (string?)quickAddButton.Attribute("VerticalAlignment"));
        Assert.Contains("Interval = TimeSpan.FromSeconds(2)", source, StringComparison.Ordinal);
        Assert.Contains("CalendarHoverPreviewTimer_OnTick", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarPreview_UsesCheckboxTimeAndWrappingContentWithExistingGaps()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var preview = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CalendarPreviewPopover");
        var row = preview
            .Descendants()
            .Single(element => element.Name.LocalName == "Grid"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CalendarPreviewTodoRow");
        var columns = row
            .Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .Select(element => (string?)element.Attribute("Width"))
            .ToList();
        var checkBox = row.Descendants().Single(element => element.Name.LocalName == "CheckBox");
        var timeText = row
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Converter={StaticResource TodoTimeDisplayConverter}}");
        var contentText = row
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Text}");

        Assert.Equal(new[] { "Auto", "12", "80", "12", "*" }, columns);
        Assert.Equal("0", (string?)checkBox.Attribute("Grid.Column"));
        Assert.Equal("QuickCompleteTodoCheckBox_OnClick", (string?)checkBox.Attribute("Click"));
        Assert.Equal("2", (string?)timeText.Attribute("Grid.Column"));
        Assert.Equal("4", (string?)contentText.Attribute("Grid.Column"));
        Assert.Equal("Wrap", (string?)contentText.Attribute("TextWrapping"));
        Assert.Equal("CalendarPreviewPopover_OnMouseLeave", (string?)preview.Attribute("MouseLeave"));
        Assert.Contains("_calendarHoverAnchor?.IsMouseOver == true || CalendarPreviewPopover.IsMouseOver", source, StringComparison.Ordinal);
        Assert.Contains("RefreshCalendarPreviewTodos();", source, StringComparison.Ordinal);
        Assert.Contains("gap: -6", source, StringComparison.Ordinal);
        Assert.Contains("var previousMargin = CalendarPreviewPopover.Margin;", source, StringComparison.Ordinal);
        Assert.Contains("CalendarPreviewPopover.Margin = new Thickness(0);", source, StringComparison.Ordinal);
        Assert.Contains("CalendarPreviewPopover.Margin = previousMargin;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CalendarMode_UsesMutedTextForDatesOutsideDisplayedMonth()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var dateNumber = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CalendarDateNumber");
        var monthTrigger = dateNumber
            .Descendants()
            .Single(element => element.Name.LocalName == "DataTrigger"
                && (string?)element.Attribute("Value") == "False");
        var multiBinding = monthTrigger
            .Descendants()
            .Single(element => element.Name.LocalName == "MultiBinding");
        var foregroundSetter = monthTrigger
            .Descendants()
            .Single(element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "Foreground");

        Assert.Equal("{StaticResource CalendarDateIsCurrentMonthConverter}", (string?)multiBinding.Attribute("Converter"));
        Assert.Equal("{StaticResource MutedTextBrush}", (string?)foregroundSetter.Attribute("Value"));
    }

    [Fact]
    public void DateCellClick_TogglesVisibleManagementPopoverForSameDate()
    {
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());

        Assert.Contains("TodoWidgetManagePopoverToggle.ShouldClose(", source, StringComparison.Ordinal);
        Assert.Contains("CloseEditPopover();", source, StringComparison.Ordinal);
        Assert.Contains("private void CloseEditPopover()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EightDayTodoList_ScrollsVerticallyWhenContentOverflows()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var scrollViewer = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "ScrollViewer"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EightDayTodoScrollViewer");

        Assert.Equal("1", (string?)scrollViewer.Attribute("Grid.Row"));
        Assert.Equal("Auto", (string?)scrollViewer.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scrollViewer.Attribute("HorizontalScrollBarVisibility"));
        Assert.Contains(scrollViewer.Descendants(), element => element.Name.LocalName == "ItemsControl");
    }

    [Fact]
    public void EightDayPreviewRows_ShowCheckboxAndWrappingTimeContentText()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var row = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Grid"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EightDayTodoPreviewRow");
        var checkBox = row
            .Descendants()
            .Single(element => element.Name.LocalName == "CheckBox");
        var previewText = row
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock");
        var itemsControl = row.Ancestors().First(element => element.Name.LocalName == "ItemsControl");

        Assert.Null(checkBox.Attribute("Content"));
        Assert.Equal("QuickCompleteTodoCheckBox_OnClick", (string?)checkBox.Attribute("Click"));
        Assert.Contains("Mode=OneWay", (string?)checkBox.Attribute("IsChecked"), StringComparison.Ordinal);
        Assert.Equal("{Binding Converter={StaticResource TodoPreviewDisplayConverter}}", (string?)previewText.Attribute("Text"));
        Assert.Equal("Wrap", (string?)previewText.Attribute("TextWrapping"));
        Assert.Equal("Stretch", (string?)itemsControl.Attribute("HorizontalContentAlignment"));
        Assert.Contains(
            previewText.Descendants(),
            element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "TextDecorations"
                && (string?)element.Attribute("Value") == "Strikethrough");
    }

    [Fact]
    public void TransientPopovers_CloseOnModeSwitchAndWindowFocusLoss()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var window = xaml.Root ?? throw new InvalidOperationException("Todo widget XAML has no root element.");

        Assert.Equal("Window_OnDeactivated", (string?)window.Attribute("Deactivated"));
        Assert.Contains("private void CloseTransientPopovers()", source, StringComparison.Ordinal);
        Assert.Contains("CloseEditPopover();", source, StringComparison.Ordinal);
        Assert.Contains("CloseQuickAddPopover();", source, StringComparison.Ordinal);
        Assert.Contains("CloseCalendarPreviewPopover();", source, StringComparison.Ordinal);
        Assert.Contains("GetForegroundWindow", source, StringComparison.Ordinal);
        Assert.Contains("CloseTransientPopoversIfFocusLost", source, StringComparison.Ordinal);
        Assert.Contains("if (foregroundWindow == _windowHandle)", source, StringComparison.Ordinal);
        Assert.Contains("_foregroundWindowWhenPopoverOpened = foregroundWindow;", source, StringComparison.Ordinal);
        Assert.True(
            source.Split("CloseTransientPopovers();", StringSplitOptions.None).Length - 1 >= 4,
            "Mode changes and focus loss should share the transient-popover close path.");
    }

    [Fact]
    public void PopoverList_ShowsTodoTimeAndUsesIconActions()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var editPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopover");
        var timeTextBlock = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Converter={StaticResource TodoTimeDisplayConverter}}");
        var editButton = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Click") == "EditTodoButton_OnClick");
        var deleteButton = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Click") == "DeleteTodoButton_OnClick");

        Assert.Equal("0", (string?)timeTextBlock.Attribute("Grid.Column"));
        Assert.Equal("✎", (string?)editButton.Attribute("Content"));
        Assert.Equal("修改", (string?)editButton.Attribute("ToolTip"));
        Assert.Equal("×", (string?)deleteButton.Attribute("Content"));
        Assert.Equal("删除", (string?)deleteButton.Attribute("ToolTip"));
    }

    [Fact]
    public void PopoverTodoRows_SeparateTimeContentAndActionsWithFixedGaps()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var editPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopover");
        var listBox = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "ListBox"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "PopoverTodoListBox");
        var rowGrid = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "Grid"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "PopoverTodoRowGrid");
        var columns = rowGrid
            .Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .Select(element => (string?)element.Attribute("Width"))
            .ToList();
        var timeText = rowGrid
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Converter={StaticResource TodoTimeDisplayConverter}}");
        var contentText = rowGrid
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Text}");
        var actionPanel = rowGrid
            .Descendants()
            .Single(element => element.Name.LocalName == "StackPanel"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "PopoverTodoActionPanel");
        var stretchSetter = listBox
            .Descendants()
            .Single(element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") == "HorizontalContentAlignment");

        Assert.Equal("Stretch", (string?)stretchSetter.Attribute("Value"));
        Assert.Equal("338", (string?)editPopover.Attribute("Width"));
        Assert.Equal("Disabled", (string?)listBox.Attribute("ScrollViewer.HorizontalScrollBarVisibility"));
        Assert.Equal("Auto", (string?)listBox.Attribute("ScrollViewer.VerticalScrollBarVisibility"));
        Assert.Equal(new[] { "80", "12", "*", "12", "52" }, columns);
        Assert.Equal("0", (string?)timeText.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)contentText.Attribute("Grid.Column"));
        Assert.Equal("Wrap", (string?)contentText.Attribute("TextWrapping"));
        Assert.Equal("4", (string?)actionPanel.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)actionPanel.Attribute("HorizontalAlignment"));
    }

    [Fact]
    public void EditPopover_UsesSeparateHourMinuteInputsAndSharedValidation()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var editPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopover");
        var names = editPopover
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBox")
            .Select(element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")))
            .Where(name => name is not null)
            .ToList();

        Assert.DoesNotContain(editPopover.Descendants(), element => element.Name.LocalName == "ComboBox");
        Assert.Contains("EditStartHourTextBox", names);
        Assert.Contains("EditStartMinuteTextBox", names);
        Assert.Contains("EditEndHourTextBox", names);
        Assert.Contains("EditEndMinuteTextBox", names);
        Assert.DoesNotContain("StartTimeTextBox", names);
        Assert.DoesNotContain("EndTimeTextBox", names);
        Assert.Contains("TodoWidgetDraftParser.ValidateQuickAddDraft(", source, StringComparison.Ordinal);
        Assert.Contains("ApplyEditValidationState(validation);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditValidation_KeepsSaveClearAndCloseButtonsAvailable()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var editPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopover");
        var saveButton = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SaveTodoButton");

        Assert.Equal("AddTodoButton_OnClick", (string?)saveButton.Attribute("Click"));
        Assert.Null(saveButton.Attribute("Visibility"));
        Assert.Contains(editPopover.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute("Content") == "清空");
        Assert.Contains(editPopover.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute("Content") == "关闭");
    }

    [Fact]
    public void EditPopover_ConstrainsExpandedEditorAndRepositionsFromDateAnchor()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var editPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopover");
        var editorScrollViewer = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "ScrollViewer"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopoverEditorScrollViewer");
        var footerPanel = editPopover
            .Descendants()
            .Single(element => element.Name.LocalName == "DockPanel"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopoverFooterPanel");

        Assert.Equal("Auto", (string?)editorScrollViewer.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)editorScrollViewer.Attribute("HorizontalScrollBarVisibility"));
        Assert.DoesNotContain(editorScrollViewer.Descendants(), element => ReferenceEquals(element, footerPanel));
        Assert.Contains("EditPopover.MaxHeight = availableSize.Height;", source, StringComparison.Ordinal);
        Assert.Contains("_editPopoverAnchor = anchor;", source, StringComparison.Ordinal);
        Assert.Contains("PositionEditPopoverNear(anchor);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditPopover_MatchesMeasuredQuickAddHeightAndAllocatesBodyRows()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var editPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditPopover");
        var rowNames = editPopover
            .Descendants()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .Select(element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")))
            .Where(name => name is not null)
            .ToList();

        Assert.Contains("PopoverTodoListRow", rowNames);
        Assert.Contains("PopoverEditorRow", rowNames);
        Assert.Contains("var quickAddHeight = MeasureQuickAddPopoverSize().Height;", source, StringComparison.Ordinal);
        Assert.Contains("EditPopover.Height = Math.Min(quickAddHeight, availableSize.Height);", source, StringComparison.Ordinal);
        Assert.Contains("private void SetPopoverEditorVisible(bool isVisible)", source, StringComparison.Ordinal);
        Assert.Contains("PopoverTodoListRow.Height", source, StringComparison.Ordinal);
        Assert.Contains("PopoverEditorRow.Height", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddPopover_IsPositionedFromClickedAddButton()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var quickAddPopover = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddPopover");

        Assert.Equal("Left", (string?)quickAddPopover.Attribute("HorizontalAlignment"));
        Assert.Equal("Top", (string?)quickAddPopover.Attribute("VerticalAlignment"));
        Assert.Contains("PositionQuickAddPopoverNear", source, StringComparison.Ordinal);
        Assert.Contains("TodoWidgetPopoverPositioner.CalculateNearAnchor", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAddPopover_PositioningMeasuresPopoverBeforeItIsShown()
    {
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());

        Assert.Contains("MeasureQuickAddPopoverSize", source, StringComparison.Ordinal);
        Assert.Contains("Visibility.Hidden", source, StringComparison.Ordinal);
        Assert.Contains("var popoverSize = MeasureQuickAddPopoverSize();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetAndPopovers_UseTheLightGreenSurfaceResources()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var styles = XDocument.Load(FindSourcePath("Resources", "Styles.xaml"));
        var backgroundColor = styles.Descendants().Single(element =>
            element.Name.LocalName == "Color"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodoWidgetBackgroundColor");
        var popoverBrush = styles.Descendants().Single(element =>
            element.Name.LocalName == "SolidColorBrush"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodoWidgetPopoverBackgroundBrush");
        var popoverNames = new[] { "QuickAddPopover", "EditPopover", "CalendarPreviewPopover" };

        Assert.Equal("#F2FAF4", backgroundColor.Value);
        Assert.Equal("#F2FAF4", (string?)popoverBrush.Attribute("Color"));
        foreach (var name in popoverNames)
        {
            var popover = xaml.Descendants().Single(element =>
                element.Name.LocalName == "Border"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == name);
            Assert.Equal("{StaticResource TodoWidgetPopoverBackgroundBrush}", (string?)popover.Attribute("Background"));
        }
    }

    [Fact]
    public void IconButtons_AreQuietWhileTextActionsKeepTheirNormalStyle()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var iconStyle = xaml.Descendants().Single(element =>
            element.Name.LocalName == "Style"
            && (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TodoWidgetIconButtonStyle");
        var setters = iconStyle.Elements()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(element => (string)element.Attribute("Property")!, element => (string?)element.Attribute("Value"));
        var iconClicks = new[]
        {
            "AddTodoForDateButton_OnClick",
            "AddTodayTodoButton_OnClick",
            "PreviousMonthButton_OnClick",
            "NextMonthButton_OnClick",
            "EditTodoButton_OnClick",
            "DeleteTodoButton_OnClick"
        };

        Assert.Equal("Transparent", setters["Background"]);
        Assert.Equal("Transparent", setters["BorderBrush"]);
        Assert.Equal("0", setters["BorderThickness"]);
        Assert.Equal("0", setters["Padding"]);
        Assert.All(
            xaml.Descendants().Where(element =>
                element.Name.LocalName == "Button"
                && iconClicks.Contains((string?)element.Attribute("Click"))),
            button => Assert.Equal("{StaticResource TodoWidgetIconButtonStyle}", (string?)button.Attribute("Style")));

        var textActions = new[] { "创建", "关闭", "新增", "清空", "本月" };
        Assert.All(
            xaml.Descendants().Where(element =>
                element.Name.LocalName == "Button"
                && textActions.Contains((string?)element.Attribute("Content"))),
            button => Assert.NotEqual("{StaticResource TodoWidgetIconButtonStyle}", (string?)button.Attribute("Style")));
    }

    [Fact]
    public void QuickAddPopover_ScrollsFormAndKeepsFooterFixed()
    {
        var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
        var quickAddPopover = xaml.Descendants().Single(element =>
            element.Name.LocalName == "Border"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddPopover");
        var layout = quickAddPopover.Elements().Single(element => element.Name.LocalName == "Grid");
        var rows = layout.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Select(element => (string?)element.Attribute("Height")).ToArray();
        var scrollViewer = layout.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddContentScrollViewer");
        var footer = layout.Descendants().Single(element =>
            element.Name.LocalName == "DockPanel"
            && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "QuickAddFooterPanel");
        var scrollNames = scrollViewer.Descendants()
            .Select(element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")))
            .Where(name => name is not null)
            .ToArray();

        Assert.Equal(new[] { "Auto", "*", "Auto" }, rows);
        Assert.Equal("1", (string?)scrollViewer.Attribute("Grid.Row"));
        Assert.Equal("Auto", (string?)scrollViewer.Attribute("VerticalScrollBarVisibility"));
        Assert.Contains("QuickAddTextBox", scrollNames);
        Assert.Contains("QuickAddTextErrorTextBlock", scrollNames);
        Assert.Contains("QuickAddTimePickerBorder", scrollNames);
        Assert.Contains("QuickAddTimeErrorTextBlock", scrollNames);
        Assert.Equal("2", (string?)footer.Attribute("Grid.Row"));
        Assert.DoesNotContain(footer.Ancestors(), element => ReferenceEquals(element, scrollViewer));
    }

    [Fact]
    public void QuickAddPopover_ConstrainsHeightBeforeMeasuring()
    {
        var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
        var methodStart = source.IndexOf("private void PositionQuickAddPopoverNear", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private Size MeasureQuickAddPopoverSize", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("QuickAddPopover.MaxHeight = availableSize.Height;", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("QuickAddPopover.MaxHeight = availableSize.Height;", StringComparison.Ordinal)
            < method.IndexOf("MeasureQuickAddPopoverSize()", StringComparison.Ordinal));
        Assert.Contains(
            "QuickAddPopover.Measure(new Size(QuickAddPopover.Width, QuickAddPopover.MaxHeight));",
            source,
            StringComparison.Ordinal);
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
