# QingJian UI Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the main QingJian notes window into a calmer, clearer writing workspace while preserving all existing note behavior.

**Architecture:** Keep the implementation inside the existing WPF presentation surface. `MainWindow.xaml` owns structure and bindings, `Styles.xaml` owns reusable visual resources, and `editor-host.css` harmonizes the hosted Toast UI editor. XML-based tests lock the important XAML contracts without introducing view-model, model, or storage changes.

**Tech Stack:** .NET 8, WPF/XAML, WebView2, Toast UI Editor CSS, xUnit, `System.Xml.Linq`.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop` during implementation.
- Do not redesign quick notes.
- Do not change storage, autosave, hotkeys, note models, or Markdown editor capabilities.
- Preserve the existing desktop todo widget, coordinator wiring, and `显示/隐藏桌面待办` main-window action; only its presentation may change.
- Keep the visual tone quiet, compact, and office-focused; avoid decorative cards and oversized type.
- Display `Note.UpdatedAt` directly as a derived view concern using XAML formatting.
- Preserve Chinese UI copy except for the approved empty-state refinement.
- Run focused tests after each task, then full tests and build before completion.

---

### Task 1: Lock Main Window Presentation Contracts

**Files:**
- Create: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`

**Interfaces:**
- Consumes: `src/QingJian.App/Views/MainWindow.xaml` and `src/QingJian.App/Resources/Styles.xaml` as XML documents.
- Produces: `MainWindowXamlTests`, which verifies metadata, list selection styling, action styles, and the editor shell.

- [ ] **Step 1: Add one failing test per presentation contract**

Create `MainWindowXamlTests` with four facts:

```csharp
[Fact]
public void NoteList_ShowsUpdatedTimeAsSecondaryMetadata()
{
    var xaml = XDocument.Load(FindMainWindowXamlPath());

    Assert.Contains(xaml.Descendants(), element =>
        element.Name.LocalName == "TextBlock"
        && ((string?)element.Attribute("Text"))?.Contains("UpdatedAt", StringComparison.Ordinal) == true
        && (string?)element.Attribute("Foreground") == "{StaticResource MutedTextBrush}");
}

[Fact]
public void NoteList_UsesDedicatedItemContainerStyleForSelectedAndHoverStates()
{
    var xaml = XDocument.Load(FindMainWindowXamlPath());
    var listBox = xaml.Descendants().Single(element =>
        element.Name.LocalName == "ListBox"
        && ((string?)element.Attribute("ItemsSource"))?.Contains("Notes", StringComparison.Ordinal) == true);
    var styles = XDocument.Load(FindStylesXamlPath());

    Assert.Equal("{StaticResource NoteListBoxItemStyle}", (string?)listBox.Attribute("ItemContainerStyle"));
    Assert.Contains(styles.Descendants(), element =>
        element.Name.LocalName == "Style"
        && (string?)FindAttributeByLocalName(element, "Key") == "NoteListBoxItemStyle");
}

[Fact]
public void MainActions_UsePurposefulButtonStyles()
{
    var xaml = XDocument.Load(FindMainWindowXamlPath());
    var newNoteButton = xaml.Descendants().First(element =>
        element.Name.LocalName == "Button" && (string?)element.Attribute("Content") == "新建便签");
    var deleteButton = xaml.Descendants().Single(element =>
        element.Name.LocalName == "Button" && (string?)element.Attribute("Content") == "删除");

    Assert.Equal("{StaticResource PrimaryButtonStyle}", (string?)newNoteButton.Attribute("Style"));
    Assert.Equal("{StaticResource SubtleDangerButtonStyle}", (string?)deleteButton.Attribute("Style"));
}

[Fact]
public void Editor_IsWrappedInReadableShell()
{
    var xaml = XDocument.Load(FindMainWindowXamlPath());
    var editorShell = xaml.Descendants().Single(element =>
        element.Name.LocalName == "Border"
        && (string?)FindAttributeByLocalName(element, "Name") == "EditorShell");

    Assert.Equal("{StaticResource EditorShellBackgroundBrush}", (string?)editorShell.Attribute("Background"));
    Assert.Equal("{StaticResource BorderBrush}", (string?)editorShell.Attribute("BorderBrush"));
    Assert.Equal("1", (string?)editorShell.Attribute("BorderThickness"));
}
```

Use directory-walking helpers based on `AppContext.BaseDirectory` to find the two source XAML files, and use `FindAttributeByLocalName` for namespaced `x:Key` and `x:Name` attributes.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test --filter FullyQualifiedName~MainWindowXamlTests
```

Expected: all four tests fail because `UpdatedAt`, `NoteListBoxItemStyle`, purposeful button styles, and `EditorShell` do not yet exist in the main-window XAML.

- [ ] **Step 3: Commit the failing presentation tests**

```powershell
git add tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs
git commit -m "test: define main window polish contracts"
```

### Task 2: Add Reusable Quiet-Workspace Styles

**Files:**
- Modify: `src/QingJian.App/Resources/Styles.xaml`

**Interfaces:**
- Consumes: existing `AppBackgroundBrush`, `PanelBackgroundBrush`, `BorderBrush`, `TextBrush`, `MutedTextBrush`, and `AccentBrush` resources.
- Produces: `HoverBackgroundBrush`, `SelectedNoteBackgroundBrush`, `EditorShellBackgroundBrush`, `DangerTextBrush`, `PrimaryButtonStyle`, `SubtleDangerButtonStyle`, and `NoteListBoxItemStyle`.

- [ ] **Step 1: Add semantic brushes**

Add these resources beside the existing application brushes:

```xml
<SolidColorBrush x:Key="HoverBackgroundBrush" Color="#F3F1ED" />
<SolidColorBrush x:Key="SelectedNoteBackgroundBrush" Color="#E4EEEC" />
<SolidColorBrush x:Key="EditorShellBackgroundBrush" Color="#FFFFFF" />
<SolidColorBrush x:Key="DangerTextBrush" Color="#9B4B43" />
```

- [ ] **Step 2: Add concrete action and list-item styles**

Add styles based on the existing implicit button style. `PrimaryButtonStyle` uses the accent background, white foreground, and accent border. `SubtleDangerButtonStyle` stays transparent with danger-colored text. `NoteListBoxItemStyle` stretches content horizontally, removes the default focus chrome, uses `Padding="12,10"` and `Margin="0,0,0,4"`, and has triggers that set hover and selected backgrounds while keeping a square, structured list treatment.

```xml
<Style x:Key="PrimaryButtonStyle"
       TargetType="Button"
       BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Background" Value="{StaticResource AccentBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="FontWeight" Value="SemiBold" />
</Style>

<Style x:Key="SubtleDangerButtonStyle"
       TargetType="Button"
       BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="Transparent" />
    <Setter Property="Foreground" Value="{StaticResource DangerTextBrush}" />
</Style>

<Style x:Key="NoteListBoxItemStyle" TargetType="ListBoxItem">
    <Setter Property="HorizontalContentAlignment" Value="Stretch" />
    <Setter Property="Padding" Value="12,10" />
    <Setter Property="Margin" Value="0,0,0,4" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="FocusVisualStyle" Value="{x:Null}" />
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="{StaticResource HoverBackgroundBrush}" />
        </Trigger>
        <Trigger Property="IsSelected" Value="True">
            <Setter Property="Background" Value="{StaticResource SelectedNoteBackgroundBrush}" />
        </Trigger>
    </Style.Triggers>
</Style>
```

- [ ] **Step 3: Run the focused tests**

Run `dotnet test --filter FullyQualifiedName~MainWindowXamlTests`.

Expected: the style-resource assertion passes; tests that require style usage in `MainWindow.xaml` remain red.

- [ ] **Step 4: Commit reusable styles**

```powershell
git add src/QingJian.App/Resources/Styles.xaml
git commit -m "style: add quiet workspace resources"
```

### Task 3: Polish the Main Window Workspace

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`

**Interfaces:**
- Consumes: all resources produced by Task 2 and existing `MainViewModel` bindings.
- Produces: the polished two-pane workspace; no new code-behind or view-model interface.

- [ ] **Step 1: Refine the window and left pane**

Keep the two-column structure, set the left column to `288`, and use `Padding="18,20"` on the left `Border`. Apply `PrimaryButtonStyle` to both new-note buttons. Keep the existing `显示/隐藏桌面待办` button and click handler in the left-pane action group, applying a quiet secondary style if needed. Set the notes `ListBox` to `ItemContainerStyle="{StaticResource NoteListBoxItemStyle}"`, disable horizontal scrolling, and remove item-template outer margins so spacing belongs to the item container.

- [ ] **Step 2: Make each note row scan cleanly**

Use a two-row `Grid` in the item template. Keep the title semibold and ellipsized. Put the content preview at row 1, column 0 with `FontSize="12"`, `MaxHeight="34"`, and wrapping. Add the update time at row 1, column 1:

```xml
<TextBlock Grid.Row="1"
           Grid.Column="1"
           Text="{Binding UpdatedAt, StringFormat={}{0:MM-dd HH:mm}}"
           Foreground="{StaticResource MutedTextBrush}"
           FontSize="11"
           Margin="12,5,0,0"
           VerticalAlignment="Top" />
```

Give the preview column remaining width and the metadata column automatic width.

- [ ] **Step 3: Refine title, actions, editor shell, and empty state**

Use `Margin="32,24,32,28"` for the editor workspace. Place the delete action in the title row and apply `SubtleDangerButtonStyle`. Reduce title size to `24`, keep semibold weight, and add a bottom border under the title region. Wrap the WebView2/fallback grid in:

```xml
<Border x:Name="EditorShell"
        Grid.Row="2"
        Background="{StaticResource EditorShellBackgroundBrush}"
        BorderBrush="{StaticResource BorderBrush}"
        BorderThickness="1">
    <Grid>
        <!-- Existing MarkdownWebView and MarkdownEditorFallback -->
    </Grid>
</Border>
```

Keep the empty state centered, constrain it to `MaxWidth="360"`, retain the clear create action, and use compact `20`-point heading type with centered supporting copy.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run:

```powershell
dotnet test --filter FullyQualifiedName~MainWindowXamlTests
```

Expected: 4 passed, 0 failed.

- [ ] **Step 5: Commit the main-window polish**

```powershell
git add src/QingJian.App/Views/MainWindow.xaml
git commit -m "feat: polish main notes workspace"
```

### Task 4: Harmonize the Hosted Markdown Editor

**Files:**
- Modify: `src/QingJian.App/EditorAssets/editor-host.css`

**Interfaces:**
- Consumes: Toast UI editor class names already used by the bundled editor.
- Produces: a white editor surface with restrained toolbar and divider colors that match WPF chrome.

- [ ] **Step 1: Align editor surface colors and boundaries**

Keep all existing font-family declarations. Set `.toastui-editor-defaultUI` to `height: 100%`, white background, and no outer border. Set toolbar background to `#faf9f7` with bottom border `#ddd7cf`. Set Markdown/WYSIWYG containers and mode switch to white, and use `#ddd7cf` for internal dividers. Do not change toolbar features, mode behavior, or JavaScript.

- [ ] **Step 2: Run the application build**

Run `dotnet build`.

Expected: build succeeds with 0 errors and 0 warnings, confirming the CSS remains packaged by the existing project configuration.

- [ ] **Step 3: Commit editor styling**

```powershell
git add src/QingJian.App/EditorAssets/editor-host.css
git commit -m "style: harmonize markdown editor surface"
```

### Task 5: Verify the Completed UI Branch

**Files:**
- Modify only if verification reveals a regression directly caused by Tasks 1-4.

**Interfaces:**
- Consumes: completed presentation changes.
- Produces: test/build evidence and a manually checked main window on `feature/ui-polish`.

- [ ] **Step 1: Run the full automated suite**

Run `dotnet test`.

Expected: all tests pass with no failed or skipped tests.

- [ ] **Step 2: Run a clean build verification**

Run `dotnet build --no-restore` after tests complete.

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Launch and inspect the WPF application**

Run:

```powershell
dotnet run --project src/QingJian.App/QingJian.App.csproj --no-build
```

Check at the default window size and near the minimum size:

- selected and hovered notes remain readable;
- long titles and previews trim or wrap without overlap;
- update times align consistently;
- delete remains secondary to new-note;
- the desktop todo toggle remains visible and functional;
- empty state is centered and usable;
- title editing, note selection, body editing, and editor loading still work.

- [ ] **Step 4: Record final branch status**

Run `git status --short --branch` and `git log --oneline -6`.

Expected: current branch is `feature/ui-polish`; `develop` remains untouched; only intentional UI branch commits are present.
