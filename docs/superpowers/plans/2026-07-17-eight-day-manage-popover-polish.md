# Eight-Day Manage Popover Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give management rows stable time/content/action regions, toggle the management popover closed on a repeated same-date click, and match its normal height to the quick-add popover.

**Architecture:** Keep rendering and positioning in `TodoWidgetWindow`, but extract the same-date close decision into a pure helper for direct unit testing. Use a five-column WPF grid for each todo row, dynamically allocate list/editor rows inside the stable-height management popover, and derive the target height from the measured cleared quick-add popover.

**Tech Stack:** WPF XAML, C#/.NET 8, xUnit, XML-based XAML structure tests.

## Global Constraints

- The `×` action remains todo deletion and retains the `删除` tooltip.
- Do not change todo persistence, sorting, quick-add validation, widget dragging, lock behavior, Z-order, or desktop behavior.
- Do not introduce WorkerW, Progman, `SetParent`, desktop-layer hosting, or `DragMove()`.
- Preserve all existing uncommitted desktop-widget changes. Do not stage or commit implementation files unless the user explicitly requests branch completion.
- Run `dotnet test` followed by `dotnet build`; do not run them in parallel.

---

### Task 1: Separate Time, Content, and Actions in Management Rows

**Files:**
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`

**Interfaces:**
- Consumes: `PopoverTodoListBox`, `TodoTimeDisplayConverter`, `EditTodoButton_OnClick`, and `DeleteTodoButton_OnClick`.
- Produces: `PopoverTodoRowGrid` with fixed `64,12,*,12,52` column widths.

- [ ] **Step 1: Add a failing row-layout test**

Add this test to `TodoWidgetWindowXamlTests`:

```csharp
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
    Assert.Equal(new[] { "64", "12", "*", "12", "52" }, columns);
    Assert.Equal("0", (string?)timeText.Attribute("Grid.Column"));
    Assert.Equal("2", (string?)contentText.Attribute("Grid.Column"));
    Assert.Equal("Wrap", (string?)contentText.Attribute("TextWrapping"));
    Assert.Equal("4", (string?)actionPanel.Attribute("Grid.Column"));
    Assert.Equal("Right", (string?)actionPanel.Attribute("HorizontalAlignment"));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetWindowXamlTests.PopoverTodoRows_SeparateTimeContentAndActionsWithFixedGaps"
```

Expected: FAIL because the current row uses `DockPanel` and has no `PopoverTodoRowGrid`.

- [ ] **Step 3: Replace the row template with a constrained grid**

Add this item-container style to `PopoverTodoListBox` so generated `ListBoxItem` containers stretch their content:

```xml
<ListBox.ItemContainerStyle>
    <Style TargetType="{x:Type ListBoxItem}">
        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
    </Style>
</ListBox.ItemContainerStyle>
```

Replace the item-template root with:

```xml
<Grid x:Name="PopoverTodoRowGrid" Margin="0,3">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="64" />
        <ColumnDefinition Width="12" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="12" />
        <ColumnDefinition Width="52" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0"
               Foreground="{StaticResource MutedTextBrush}"
               VerticalAlignment="Top"
               Text="{Binding Converter={StaticResource TodoTimeDisplayConverter}}" />
    <TextBlock Grid.Column="2"
               Text="{Binding Text}"
               TextWrapping="Wrap"
               VerticalAlignment="Top" />
    <StackPanel x:Name="PopoverTodoActionPanel"
                Grid.Column="4"
                Orientation="Horizontal"
                HorizontalAlignment="Right"
                VerticalAlignment="Top">
        <Button Content="✎"
                ToolTip="修改"
                Width="24"
                Height="24"
                MinHeight="0"
                Padding="0"
                Tag="{Binding}"
                Click="EditTodoButton_OnClick" />
        <Button Content="×"
                ToolTip="删除"
                Width="24"
                Height="24"
                MinHeight="0"
                Padding="0"
                Tag="{Binding}"
                Margin="4,0,0,0"
                Click="DeleteTodoButton_OnClick" />
    </StackPanel>
</Grid>
```

- [ ] **Step 4: Run all XAML structure tests and verify GREEN**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetWindowXamlTests"
```

Expected: every `TodoWidgetWindowXamlTests` test passes.

---

### Task 2: Toggle the Same Date's Management Popover Closed

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoWidgetManagePopoverToggle.cs`
- Create: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetManagePopoverToggleTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`

**Interfaces:**
- Produces: `TodoWidgetManagePopoverToggle.ShouldClose(bool isVisible, DateOnly? pinnedDate, DateOnly clickedDate) : bool`.
- Consumes: `TodoWidgetViewModel.PinnedDate`, `EditPopover.Visibility`, `ClearTodoForm`, and `ResetEditValidationState`.

- [ ] **Step 1: Add failing pure toggle tests**

Create `TodoWidgetManagePopoverToggleTests.cs`:

```csharp
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetManagePopoverToggleTests
{
    private static readonly DateOnly SelectedDate = new(2026, 7, 17);

    [Fact]
    public void ShouldClose_ReturnsTrueForVisiblePopoverPinnedToClickedDate()
    {
        Assert.True(TodoWidgetManagePopoverToggle.ShouldClose(true, SelectedDate, SelectedDate));
    }

    [Fact]
    public void ShouldClose_ReturnsFalseWhenPopoverIsHidden()
    {
        Assert.False(TodoWidgetManagePopoverToggle.ShouldClose(false, SelectedDate, SelectedDate));
    }

    [Fact]
    public void ShouldClose_ReturnsFalseForDifferentPinnedDate()
    {
        Assert.False(TodoWidgetManagePopoverToggle.ShouldClose(true, SelectedDate.AddDays(1), SelectedDate));
    }
}
```

- [ ] **Step 2: Run the pure tests and verify RED**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetManagePopoverToggleTests"
```

Expected: build failure because `TodoWidgetManagePopoverToggle` does not exist.

- [ ] **Step 3: Add the minimal pure toggle helper**

Create `TodoWidgetManagePopoverToggle.cs`:

```csharp
namespace QingJian.App.TodoWidgets;

public static class TodoWidgetManagePopoverToggle
{
    public static bool ShouldClose(bool isVisible, DateOnly? pinnedDate, DateOnly clickedDate)
    {
        return isVisible && pinnedDate == clickedDate;
    }
}
```

- [ ] **Step 4: Run the pure tests and verify GREEN**

Run the same filtered command. Expected: 3 tests pass.

- [ ] **Step 5: Add a failing window-integration structure test**

Add:

```csharp
[Fact]
public void DateCellClick_TogglesVisibleManagementPopoverForSameDate()
{
    var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());

    Assert.Contains("TodoWidgetManagePopoverToggle.ShouldClose(", source, StringComparison.Ordinal);
    Assert.Contains("CloseEditPopover();", source, StringComparison.Ordinal);
    Assert.Contains("private void CloseEditPopover()", source, StringComparison.Ordinal);
}
```

Run:

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetWindowXamlTests.DateCellClick_TogglesVisibleManagementPopoverForSameDate"
```

Expected: FAIL because the window does not call the helper or have `CloseEditPopover`.

- [ ] **Step 6: Integrate the toggle and centralize close cleanup**

Update the date-cell handler:

```csharp
private void DateCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is not FrameworkElement { DataContext: DateOnly date } anchor)
    {
        return;
    }

    if (TodoWidgetManagePopoverToggle.ShouldClose(
            EditPopover.Visibility == Visibility.Visible,
            _viewModel.PinnedDate,
            date))
    {
        CloseEditPopover();
        return;
    }

    _viewModel.PinDate(date);
    ShowTodoManagePopover(anchor, showEditor: false);
}
```

Centralize close behavior:

```csharp
private void CloseEditPopover()
{
    _viewModel.ClearPinnedDate();
    _editPopoverAnchor = null;
    EditPopover.Visibility = Visibility.Collapsed;
    SetPopoverEditorVisible(false);
    ClearTodoForm();
    ResetEditValidationState();
}

private void ClosePopoverButton_OnClick(object sender, RoutedEventArgs e)
{
    CloseEditPopover();
}
```

`SetPopoverEditorVisible` is introduced in Task 3. Until Task 3 is applied, use `PopoverEditorVisibility = Visibility.Collapsed` in `CloseEditPopover`, then replace it during Task 3.

- [ ] **Step 7: Run toggle and window tests**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetManagePopoverToggleTests|FullyQualifiedName~TodoWidgetWindowXamlTests"
```

Expected: all selected tests pass.

---

### Task 3: Match Management Height to Quick Add and Allocate Body Space

**Files:**
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`

**Interfaces:**
- Consumes: `MeasureQuickAddPopoverSize`, `PopoverEditorVisibility`, `PopoverTodoListBox`, and `EditPopoverEditorScrollViewer`.
- Produces: `PopoverTodoListRow`, `PopoverEditorRow`, and `SetPopoverEditorVisible(bool isVisible)`.

- [ ] **Step 1: Add failing height and row-allocation tests**

Add:

```csharp
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
```

- [ ] **Step 2: Run the single test and verify RED**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetWindowXamlTests.EditPopover_MatchesMeasuredQuickAddHeightAndAllocatesBodyRows"
```

Expected: FAIL because the named row definitions, measured quick-add height assignment, and row-allocation helper do not exist.

- [ ] **Step 3: Name the body rows and give list mode the remaining height**

Change the management grid rows to:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition x:Name="PopoverTodoListRow" Height="*" />
    <RowDefinition x:Name="PopoverEditorRow" Height="0" />
</Grid.RowDefinitions>
```

Remove the fixed `MaxHeight="72"` from `PopoverTodoListBox` and keep its native vertical scrolling.

- [ ] **Step 4: Add stable height calculation and editor row allocation**

In `ShowTodoManagePopover`, clear stale quick-add validation before measuring and set the target height:

```csharp
QuickAddPopover.Visibility = Visibility.Collapsed;
ClearQuickAddForm();
ResetQuickAddValidationState();

var contentTop = MainLayoutGrid.RowDefinitions[0].ActualHeight;
var availableSize = new Size(
    MainLayoutGrid.ActualWidth,
    Math.Max(0, MainLayoutGrid.ActualHeight - contentTop));
var quickAddHeight = MeasureQuickAddPopoverSize().Height;
EditPopover.Height = Math.Min(quickAddHeight, availableSize.Height);
EditPopover.MaxHeight = availableSize.Height;
SetPopoverEditorVisible(showEditor);
```

Add:

```csharp
private void SetPopoverEditorVisible(bool isVisible)
{
    PopoverEditorVisibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    PopoverTodoListRow.Height = isVisible ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
    PopoverEditorRow.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    PopoverTodoListBox.MaxHeight = isVisible ? 72 : double.PositiveInfinity;
}
```

Replace direct assignments to `PopoverEditorVisibility` in `ShowTodoManagePopover`, `AddTodoButton_OnClick`, `EditTodoButton_OnClick`, and `CloseEditPopover` with `SetPopoverEditorVisible`.

- [ ] **Step 5: Run all desktop-widget tests and verify GREEN**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidget"
```

Expected: all desktop-widget tests pass.

---

### Task 4: Full Verification and Review

**Files:**
- Verify all modified and untracked desktop-widget files.

**Interfaces:**
- Consumes: completed Tasks 1 through 3.
- Produces: a test- and build-verified working tree ready for user UI review.

- [ ] **Step 1: Run the full test suite**

```powershell
dotnet test
```

Expected: zero failed tests.

- [ ] **Step 2: Build the application**

```powershell
dotnet build
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Inspect final scope and removed legacy layout**

```powershell
git diff --check
git status --short --branch
rg -n "PopoverTodoRowGrid|TodoWidgetManagePopoverToggle|SetPopoverEditorVisible|MeasureQuickAddPopoverSize" src/QingJian.App/TodoWidgets tests/QingJian.App.Tests/TodoWidgets
```

Expected: no whitespace errors; branch remains `feature/desktop-todo-widget`; the new row, toggle, and height paths are present.

- [ ] **Step 4: Request a read-only code review**

Ask the reviewer to focus on long-text wrapping, event bubbling for same-date clicks, list/editor row allocation, height clamping, and preservation of delete semantics. Fix every Critical or Important finding, then rerun Steps 1 and 2.
