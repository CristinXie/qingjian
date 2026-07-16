# Desktop Todo Widget Interaction Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace legacy todo edit time controls, remove date-cell hover previews, and make overflowing 8-day todo lists vertically scrollable.

**Architecture:** Keep the existing `TodoWidgetWindow` and shared `TodoWidgetDraftParser`. Remove preview-only window state, let date clicks always open management mode, wrap only the 8-day todo body in a `ScrollViewer`, and route edit saves through the same four-field validation API already used by quick add.

**Tech Stack:** WPF XAML, C#/.NET 8, xUnit, XML-based XAML structure tests.

## Global Constraints

- Do not introduce WorkerW, Progman, `SetParent`, desktop-layer hosting, or `DragMove()`.
- Keep the current borderless WPF window, manual drag behavior, Z-order behavior, persistence, sorting, and shared todo data unchanged.
- The 8-day `+` button must continue to stop the date-cell click from opening the management popover.
- Run `dotnet test` and then `dotnet build`; do not run them in parallel.

---

### Task 1: Remove Hover Preview and Add 8-Day Cell Scrolling

**Files:**
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`

**Interfaces:**
- Consumes: `DateCell_OnMouseLeftButtonDown`, `ShowTodoManagePopover`, and the existing date-bound 8-day `ItemsControl`.
- Produces: an `EightDayTodoScrollViewer` with vertical auto-scrolling and click-only date popovers.

- [ ] **Step 1: Replace hover-preview tests with failing click-only and scrolling tests**

Replace `DateCells_StartDelayedPreviewOnHoverAndClosePreviewOnLeave` and `Popover_HasPreviewAndManageModes`, and update the quick-add assertion:

```csharp
[Fact]
public void DateCells_OpenManagementPopoverOnlyOnClick()
{
    var xaml = XDocument.Load(FindTodoWidgetWindowXamlPath());
    var source = File.ReadAllText(FindTodoWidgetWindowCodeBehindPath());
    var dateCells = xaml
        .Descendants()
        .Where(element => element.Name.LocalName == "Border"
            && (string?)element.Attribute("MouseLeftButtonDown") == "DateCell_OnMouseLeftButtonDown")
        .ToList();

    Assert.Equal(2, dateCells.Count);
    Assert.All(dateCells, element =>
    {
        Assert.Null(element.Attribute("MouseEnter"));
        Assert.Null(element.Attribute("MouseLeave"));
    });
    Assert.DoesNotContain("_hoverPreviewTimer", source, StringComparison.Ordinal);
    Assert.DoesNotContain("ShowTodoPreviewPopover", source, StringComparison.Ordinal);
    Assert.DoesNotContain("ClosePreviewPopover", source, StringComparison.Ordinal);
    Assert.Contains("ShowTodoManagePopover(anchor, showEditor: false);", source, StringComparison.Ordinal);
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
```

In `EightDayCells_HaveQuickAddButtonForTheDate`, replace the hover-timer assertion with:

```csharp
Assert.Contains("e.Handled = true;", source, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetWindowXamlTests"
```

Expected: FAIL because hover attributes and timer methods still exist and `EightDayTodoScrollViewer` does not exist.

- [ ] **Step 3: Implement click-only date cells and the scrolling todo body**

In both date-cell borders in `TodoWidgetWindow.xaml`, remove:

```xml
MouseEnter="DateCell_OnMouseEnter"
MouseLeave="DateCell_OnMouseLeave"
```

Wrap the 8-day cell's todo `ItemsControl`:

```xml
<ScrollViewer x:Name="EightDayTodoScrollViewer"
              Grid.Row="1"
              VerticalScrollBarVisibility="Auto"
              HorizontalScrollBarVisibility="Disabled"
              PanningMode="VerticalOnly">
    <ItemsControl>
        <ItemsControl.ItemsSource>
            <MultiBinding Converter="{StaticResource TodoItemsForDateConverter}">
                <Binding />
                <Binding Path="DataContext.VisibleTodos"
                         RelativeSource="{RelativeSource AncestorType=Window}" />
            </MultiBinding>
        </ItemsControl.ItemsSource>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <CheckBox Content="{Binding Text}"
                          IsChecked="{Binding IsCompleted, Mode=OneWay}"
                          Tag="{Binding}"
                          Click="QuickCompleteTodoCheckBox_OnClick"
                          FontSize="11" />
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</ScrollViewer>
```

In `TodoWidgetWindow.xaml.cs`, delete the hover timer field, hover date/anchor fields, constructor initialization, closing cleanup, hover event handlers, preview show/close methods, and `StopHoverPreviewTimer`. Remove calls to `StopHoverPreviewTimer()` from date click and quick add. Delete preview-only management state and make the action panel always visible by removing its visibility binding and the `SetPopoverManagementMode` calls.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the same filtered test command. Expected: all `TodoWidgetWindowXamlTests` pass.

- [ ] **Step 5: Review the diff for interaction scope**

Run:

```powershell
git diff --check
git diff -- src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs
```

Expected: no whitespace errors; quick add still sets `e.Handled = true`; no drag, Z-order, persistence, or sorting code changes.

---

### Task 2: Use Separate Hour and Minute Inputs When Editing

**Files:**
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml.cs`
- Reuse without behavior changes: `src/QingJian.App/TodoWidgets/TodoWidgetDraftParser.cs`

**Interfaces:**
- Consumes: `TodoWidgetDraftParser.ValidateQuickAddDraft(DateOnly, string, string?, string?, string?, string?)` and `TodoWidgetQuickAddDraftValidation`.
- Produces: `EditStartHourTextBox`, `EditStartMinuteTextBox`, `EditEndHourTextBox`, `EditEndMinuteTextBox`, and `EditTimePickerBorder`.

- [ ] **Step 1: Add failing edit-form structure and validation-path tests**

Add:

```csharp
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
```

- [ ] **Step 2: Run focused tests and verify RED**

Run the filtered `TodoWidgetWindowXamlTests` command. Expected: FAIL because the old combo box and `HH:mm` text boxes still exist and edit save still calls `CreateDraft`.

- [ ] **Step 3: Replace the legacy edit controls**

Mirror the quick-add time layout inside `PopoverEditorPanel`, using these names:

```xml
<TextBlock Text="时间"
           FontSize="12"
           Foreground="{StaticResource MutedTextBrush}"
           Margin="0,8,0,4" />
<Border x:Name="EditTimePickerBorder"
        BorderBrush="{StaticResource TodoWidgetBorderBrush}"
        BorderThickness="1"
        CornerRadius="4"
        Background="#FFFFFF"
        Padding="6,5">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="16" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <StackPanel Grid.Column="0">
            <TextBlock Text="开始"
                       FontSize="11"
                       Foreground="{StaticResource MutedTextBrush}"
                       Margin="0,0,0,4" />
            <StackPanel Orientation="Horizontal">
                <TextBox x:Name="EditStartHourTextBox"
                         Width="38"
                         Height="34"
                         MaxLength="2"
                         Padding="0"
                         BorderThickness="1"
                         BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                         Background="#FFFFFF"
                         TextAlignment="Center"
                         VerticalContentAlignment="Center"
                         AcceptsReturn="False"
                         TextWrapping="NoWrap"
                         VerticalScrollBarVisibility="Disabled"
                         ToolTip="小时"
                         TextChanged="EditField_OnChanged" />
                <TextBlock Text=":" Margin="5,0" FontSize="18" VerticalAlignment="Center" />
                <TextBox x:Name="EditStartMinuteTextBox"
                         Width="38"
                         Height="34"
                         MaxLength="2"
                         Padding="0"
                         BorderThickness="1"
                         BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                         Background="#FFFFFF"
                         TextAlignment="Center"
                         VerticalContentAlignment="Center"
                         AcceptsReturn="False"
                         TextWrapping="NoWrap"
                         VerticalScrollBarVisibility="Disabled"
                         ToolTip="分钟"
                         TextChanged="EditField_OnChanged" />
            </StackPanel>
        </StackPanel>
        <TextBlock Grid.Column="1" Text="-" HorizontalAlignment="Center" VerticalAlignment="Center" />
        <StackPanel Grid.Column="2">
            <TextBlock Text="结束"
                       FontSize="11"
                       Foreground="{StaticResource MutedTextBrush}"
                       Margin="0,0,0,4" />
            <StackPanel Orientation="Horizontal">
                <TextBox x:Name="EditEndHourTextBox"
                         Width="38"
                         Height="34"
                         MaxLength="2"
                         Padding="0"
                         BorderThickness="1"
                         BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                         Background="#FFFFFF"
                         TextAlignment="Center"
                         VerticalContentAlignment="Center"
                         AcceptsReturn="False"
                         TextWrapping="NoWrap"
                         VerticalScrollBarVisibility="Disabled"
                         ToolTip="小时"
                         TextChanged="EditField_OnChanged" />
                <TextBlock Text=":" Margin="5,0" FontSize="18" VerticalAlignment="Center" />
                <TextBox x:Name="EditEndMinuteTextBox"
                         Width="38"
                         Height="34"
                         MaxLength="2"
                         Padding="0"
                         BorderThickness="1"
                         BorderBrush="{StaticResource TodoWidgetBorderBrush}"
                         Background="#FFFFFF"
                         TextAlignment="Center"
                         VerticalContentAlignment="Center"
                         AcceptsReturn="False"
                         TextWrapping="NoWrap"
                         VerticalScrollBarVisibility="Disabled"
                         ToolTip="分钟"
                         TextChanged="EditField_OnChanged" />
            </StackPanel>
        </StackPanel>
    </Grid>
</Border>
```

Keep `TodoTextBox`, `PopoverErrorTextBlock`, save, clear, and close controls visible. Change the edit-mode save button label to `保存` and retain `新增` only when the editor is opened for a new todo from the Today entry point.

- [ ] **Step 4: Route edit saves through shared validation**

Replace `CreateDraftFromPopover` usage with:

```csharp
var validation = TodoWidgetDraftParser.ValidateQuickAddDraft(
    _viewModel.ActivePopoverDate,
    TodoTextBox.Text,
    EditStartHourTextBox.Text,
    EditStartMinuteTextBox.Text,
    EditEndHourTextBox.Text,
    EditEndMinuteTextBox.Text);

ApplyEditValidationState(validation);
if (!validation.IsValid || validation.Draft is null)
{
    return;
}
```

Populate existing todos with `todo.StartTime?.ToString("HH")`, `todo.StartTime?.ToString("mm")`, `todo.EndTime?.ToString("HH")`, and `todo.EndTime?.ToString("mm")`. Clear all four values in `ClearTodoForm`.

Implement `ApplyEditValidationState` so `TodoTextBox` is red only for `TextError`, `EditTimePickerBorder` is red only for `TimeError`, and `PopoverErrorTextBlock` displays all present validation messages. Implement `EditField_OnChanged` to restore normal borders and hide stale errors without changing button visibility.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the filtered test command. Expected: all `TodoWidgetWindowXamlTests` pass.

- [ ] **Step 6: Run parser tests to confirm shared rules remain green**

Run:

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetDraftParserTests"
```

Expected: all no-time, single-time, range, incomplete time point, invalid range, and empty text tests pass.

---

### Task 3: Full Verification

**Files:**
- Verify: all modified and untracked desktop todo widget files.

**Interfaces:**
- Consumes: completed Tasks 1 and 2.
- Produces: a test- and build-verified working tree ready for user review.

- [ ] **Step 1: Run all automated tests**

```powershell
dotnet test
```

Expected: all tests pass with zero failed tests.

- [ ] **Step 2: Build the application**

```powershell
dotnet build
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 3: Inspect final scope**

```powershell
git diff --check
git status --short --branch
git diff --stat
```

Expected: no whitespace errors, current branch remains `feature/desktop-todo-widget`, and changes are limited to the existing desktop todo feature work plus this interaction update.
