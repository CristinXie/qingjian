# Eight-Day Todo Preview Row Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. The user explicitly requested inline execution without subagents.

**Goal:** Render each 8-day todo preview as an independent checkbox followed by one wrapping time-and-content string.

**Architecture:** Add a dedicated WPF value converter that composes the existing time display with todo text. Replace the checkbox-content template with a two-column row so completion remains independently clickable while the display text receives a constrained width and wraps.

**Tech Stack:** WPF XAML, C#/.NET 8, xUnit, XML-based XAML structure tests.

## Global Constraints

- Do not use subagents.
- Preserve todo completion, sorting, persistence, and shared data behavior.
- Preserve vertical scrolling and keep horizontal scrolling disabled.
- Do not stage or commit implementation files unless the user requests branch completion.
- Run `dotnet test` followed by `dotnet build`; do not run them in parallel.

---

### Task 1: Compact Preview Display Converter

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoPreviewDisplayConverter.cs`
- Create: `tests/QingJian.App.Tests/TodoWidgets/TodoPreviewDisplayConverterTests.cs`

**Interfaces:**
- Consumes: `TodoItem`, `TodoTimeDisplayConverter`.
- Produces: `TodoPreviewDisplayConverter : IValueConverter`.

- [ ] **Step 1: Add failing converter tests**

```csharp
using System.Globalization;
using QingJian.App.Models;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoPreviewDisplayConverterTests
{
    private readonly TodoPreviewDisplayConverter _converter = new();

    [Fact]
    public void Convert_ReturnsOnlyContentForNoTimeTodo()
    {
        var todo = new TodoItem { Text = "整理资料" };

        Assert.Equal("整理资料", Convert(todo));
    }

    [Fact]
    public void Convert_PrefixesSingleTime()
    {
        var todo = new TodoItem { Text = "开会", StartTime = new TimeOnly(9, 30) };

        Assert.Equal("09:30 开会", Convert(todo));
    }

    [Fact]
    public void Convert_PrefixesTimeRange()
    {
        var todo = new TodoItem
        {
            Text = "专注工作",
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 45)
        };

        Assert.Equal("09:30-10:45 专注工作", Convert(todo));
    }

    private string Convert(TodoItem todo)
    {
        return (string)_converter.Convert(todo, typeof(string), null!, CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoPreviewDisplayConverterTests"
```

Expected: build failure because `TodoPreviewDisplayConverter` does not exist.

- [ ] **Step 3: Add the minimal converter**

```csharp
using System.Globalization;
using System.Windows.Data;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public sealed class TodoPreviewDisplayConverter : IValueConverter
{
    private readonly TodoTimeDisplayConverter _timeConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoItem todo)
        {
            return string.Empty;
        }

        var time = (string)_timeConverter.Convert(todo, typeof(string), parameter, culture);
        return string.IsNullOrEmpty(time) ? todo.Text : $"{time} {todo.Text}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 4: Verify GREEN**

Run the same filtered test command. Expected: 3 tests pass.

---

### Task 2: Eight-Day Preview Row Layout

**Files:**
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`

**Interfaces:**
- Consumes: `TodoPreviewDisplayConverter`, `QuickCompleteTodoCheckBox_OnClick`, and `EightDayTodoScrollViewer`.
- Produces: `EightDayTodoPreviewRow` with separate checkbox and wrapping text.

- [ ] **Step 1: Add a failing XAML structure test**

```csharp
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
    var text = row
        .Descendants()
        .Single(element => element.Name.LocalName == "TextBlock");
    var itemsControl = row.Ancestors().First(element => element.Name.LocalName == "ItemsControl");

    Assert.Null(checkBox.Attribute("Content"));
    Assert.Equal("QuickCompleteTodoCheckBox_OnClick", (string?)checkBox.Attribute("Click"));
    Assert.Contains("Mode=OneWay", (string?)checkBox.Attribute("IsChecked"), StringComparison.Ordinal);
    Assert.Equal("{Binding Converter={StaticResource TodoPreviewDisplayConverter}}", (string?)text.Attribute("Text"));
    Assert.Equal("Wrap", (string?)text.Attribute("TextWrapping"));
    Assert.Equal("Stretch", (string?)itemsControl.Attribute("HorizontalContentAlignment"));
}
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidgetWindowXamlTests.EightDayPreviewRows_ShowCheckboxAndWrappingTimeContentText"
```

Expected: FAIL because `EightDayTodoPreviewRow` does not exist.

- [ ] **Step 3: Register the converter and replace the item template**

Add to window resources:

```xml
<todo:TodoPreviewDisplayConverter x:Key="TodoPreviewDisplayConverter" />
```

Set `HorizontalContentAlignment="Stretch"` on the inner 8-day `ItemsControl`, then use:

```xml
<DataTemplate>
    <Grid x:Name="EightDayTodoPreviewRow" Margin="0,1">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="4" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <CheckBox Grid.Column="0"
                  IsChecked="{Binding IsCompleted, Mode=OneWay}"
                  Tag="{Binding}"
                  Click="QuickCompleteTodoCheckBox_OnClick"
                  VerticalAlignment="Top" />
        <TextBlock Grid.Column="2"
                   Text="{Binding Converter={StaticResource TodoPreviewDisplayConverter}}"
                   TextWrapping="Wrap"
                   FontSize="11"
                   VerticalAlignment="Top" />
    </Grid>
</DataTemplate>
```

- [ ] **Step 4: Verify widget tests**

```powershell
dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj --filter "FullyQualifiedName~TodoWidget"
```

Expected: all selected tests pass.

---

### Task 3: Full Verification

- [ ] **Step 1: Run all tests**

```powershell
dotnet test
```

Expected: zero failed tests.

- [ ] **Step 2: Build**

```powershell
dotnet build
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Check scope**

```powershell
git diff --check
git status --short --branch
```

Expected: no whitespace errors and branch remains `feature/desktop-todo-widget`.
