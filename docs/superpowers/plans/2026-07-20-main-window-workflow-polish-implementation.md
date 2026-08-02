# QingJian Main Window Workflow Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add protected note deletion, recency-grouped navigation, creation metadata, direct title-to-body focus, editor undo/redo/mode actions, `Ctrl+Y`, and Chinese editor prompts to the polished main window.

**Architecture:** Keep note persistence unchanged. Add pure date/grouping helpers and a grouped `ListCollectionView` over the existing `Notes` collection, then bridge WPF icon actions to focused functions exposed by `window.qingjianEditor`. Main-window code-behind owns confirmation and focus because they are view concerns; Toast UI owns editor history and mode state.

**Tech Stack:** .NET 8, WPF/XAML, `ListCollectionView`, WebView2, Toast UI Editor, JavaScript, CSS, xUnit, XML/source contract tests.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop` during implementation.
- Preserve the existing 8 DIP rounded styles, desktop todo toggle, quick-note behavior, note schema, autosave, attachments, and Markdown storage.
- Use local time for navigation grouping and creation-date display.
- Keep `Markdown` in English; translate other newly visible prompts and Toast UI labels to Chinese.
- Use four 32 DIP icon buttons in the order mode, undo, redo, delete, with 8 DIP adjacent spacing and immediate tooltips.
- Run .NET test/build commands serially with `-c Release` because a Debug application instance may be running.

---

### Task 1: Add Date Grouping and Creation-Date Formatting

**Files:**
- Create: `src/QingJian.App/ViewModels/NoteNavigationDateHelper.cs`
- Create: `src/QingJian.App/ViewModels/NoteNavigationGroupDescription.cs`
- Create: `src/QingJian.App/Views/NoteCreatedAtDisplayConverter.cs`
- Create: `tests/QingJian.App.Tests/ViewModels/NoteNavigationDateHelperTests.cs`

**Interfaces:**
- Produces: `NoteNavigationDateHelper.GetGroupName(DateTime, DateTime)`, `NoteNavigationDateHelper.FormatCreatedAt(DateTime, DateTime)`, `NoteNavigationGroupDescription(Func<DateTime>)`, and `NoteCreatedAtDisplayConverter`.
- Consumes: `QingJian.App.Models.Note` and local current time.

- [ ] **Step 1: Write failing date helper tests**

Create tests with a fixed local current time of `2026-07-20 12:00`:

```csharp
[Theory]
[InlineData(2026, 7, 20, "今天")]
[InlineData(2026, 7, 21, "今天")]
[InlineData(2026, 7, 19, "过去30天")]
[InlineData(2026, 6, 20, "过去30天")]
[InlineData(2026, 6, 19, "6月")]
[InlineData(2026, 1, 5, "1月")]
[InlineData(2025, 12, 31, "2025年")]
public void GetGroupName_UsesAgreedLocalDateBuckets(int year, int month, int day, string expected)
{
    var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
    var updatedAt = new DateTime(year, month, day, 8, 0, 0, DateTimeKind.Local);

    Assert.Equal(expected, NoteNavigationDateHelper.GetGroupName(updatedAt, now));
}

[Theory]
[InlineData(2026, 1, 2, "创建于 1月2日")]
[InlineData(2025, 12, 3, "创建于 2025年12月3日")]
public void FormatCreatedAt_UsesYearSensitiveChineseFormat(int year, int month, int day, string expected)
{
    var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
    var createdAt = new DateTime(year, month, day, 8, 0, 0, DateTimeKind.Local);

    Assert.Equal(expected, NoteNavigationDateHelper.FormatCreatedAt(createdAt, now));
}
```

- [ ] **Step 2: Verify RED**

Run `dotnet test -c Release --filter FullyQualifiedName~NoteNavigationDateHelperTests`.

Expected: compile failure because the helper does not exist.

- [ ] **Step 3: Implement the pure helper and WPF adapters**

Implement:

```csharp
public static class NoteNavigationDateHelper
{
    public static string GetGroupName(DateTime updatedAt, DateTime localNow)
    {
        var date = ToLocal(updatedAt).Date;
        var today = ToLocal(localNow).Date;

        if (date >= today) return "今天";
        if (date >= today.AddDays(-30)) return "过去30天";
        if (date.Year == today.Year) return $"{date.Month}月";
        return $"{date.Year}年";
    }

    public static string FormatCreatedAt(DateTime createdAt, DateTime localNow)
    {
        var date = ToLocal(createdAt);
        var now = ToLocal(localNow);
        return date.Year == now.Year
            ? $"创建于 {date.Month}月{date.Day}日"
            : $"创建于 {date.Year}年{date.Month}月{date.Day}日";
    }

    private static DateTime ToLocal(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
    }
}
```

`NoteNavigationGroupDescription` overrides `GroupNameFromItem` and returns `GetGroupName(note.UpdatedAt, _localNow())`. `NoteCreatedAtDisplayConverter.Convert` returns `FormatCreatedAt(createdAt, DateTime.Now)` and returns an empty string for non-`DateTime` values; `ConvertBack` throws `NotSupportedException`.

- [ ] **Step 4: Verify GREEN and commit**

Run `dotnet test -c Release --filter FullyQualifiedName~NoteNavigationDateHelperTests`.

Expected: all date tests pass.

```powershell
git add src/QingJian.App/ViewModels/NoteNavigationDateHelper.cs src/QingJian.App/ViewModels/NoteNavigationGroupDescription.cs src/QingJian.App/Views/NoteCreatedAtDisplayConverter.cs tests/QingJian.App.Tests/ViewModels/NoteNavigationDateHelperTests.cs
git commit -m "feat: add note navigation date grouping"
```

### Task 2: Expose and Refresh the Grouped Note View

**Files:**
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`
- Modify: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `NoteNavigationGroupDescription` from Task 1.
- Produces: `ListCollectionView NotesView` and `public void RefreshNoteNavigation()`.

- [ ] **Step 1: Write failing grouped-view tests**

Add tests that construct `MainViewModel` with a fixed local clock and notes from today, yesterday, an older current-year month, and an earlier year. Assert:

```csharp
var groupNames = viewModel.NotesView.Groups!
    .Cast<CollectionViewGroup>()
    .Select(group => group.Name)
    .ToArray();

Assert.Equal(new object[] { "今天", "过去30天", "6月", "2025年" }, groupNames);
Assert.Equal(new[] { "today-new", "today-old", "yesterday", "june", "last-year" },
    viewModel.NotesView.Cast<Note>().Select(note => note.Id));
```

Add a save-refresh test whose fake service changes `UpdatedAt` to today during `SaveNoteAsync`; after `SaveSelectedNoteNowAsync`, assert the note moved into the `今天` group.

- [ ] **Step 2: Verify RED**

Run `dotnet test -c Release --filter FullyQualifiedName~MainViewModelTests`.

Expected: compile failure because `NotesView` and the clock-aware constructor do not exist.

- [ ] **Step 3: Implement `NotesView`**

Add a constructor overload:

```csharp
public MainViewModel(INoteService noteService, TimeSpan autoSaveDelay, Func<DateTime> localNow)
```

Existing constructors delegate to it with `() => DateTime.Now`. Initialize:

```csharp
NotesView = new ListCollectionView(Notes);
NotesView.SortDescriptions.Add(new SortDescription(nameof(Note.UpdatedAt), ListSortDirection.Descending));
NotesView.GroupDescriptions.Add(new NoteNavigationGroupDescription(localNow));
```

Expose:

```csharp
public ListCollectionView NotesView { get; }
public void RefreshNoteNavigation() => NotesView.Refresh();
```

Call `RefreshNoteNavigation()` after load, create, delete, `AddSavedNote`, and successful save. Keep existing `Notes.Move` behavior for compatibility with current tests.

- [ ] **Step 4: Verify GREEN and commit**

Run `dotnet test -c Release --filter FullyQualifiedName~MainViewModelTests`.

Expected: all MainViewModel tests pass.

```powershell
git add src/QingJian.App/ViewModels/MainViewModel.cs tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat: group note navigation by recency"
```

### Task 3: Add Toast UI Commands, Redo Shortcut, and Chinese Text

**Files:**
- Modify: `src/QingJian.App/EditorAssets/editor-host.js`
- Modify: `src/QingJian.App/EditorAssets/editor-host.css`
- Create: `tests/QingJian.App.Tests/Editor/EditorHostAssetTests.cs`

**Interfaces:**
- Produces: `window.qingjianEditor.undo()`, `redo()`, `toggleMode()`, and `focus()`.
- Preserves: existing `setMarkdown`, `getMarkdown`, `setEditorMode`, image upload, paste, and link behavior.

- [ ] **Step 1: Write failing asset contract tests**

Read `editor-host.js` and `editor-host.css` as text and assert the source contains:

```csharp
Assert.Contains("hideModeSwitch: true", script, StringComparison.Ordinal);
Assert.Contains("language: \"zh-CN\"", script, StringComparison.Ordinal);
Assert.Contains("undo: function", script, StringComparison.Ordinal);
Assert.Contains("redo: function", script, StringComparison.Ordinal);
Assert.Contains("toggleMode: function", script, StringComparison.Ordinal);
Assert.Contains("focus: function", script, StringComparison.Ordinal);
Assert.Contains("event.key.toLowerCase() === \"y\"", script, StringComparison.Ordinal);
Assert.Contains("切换到所见即所得", script, StringComparison.Ordinal);
Assert.Contains("Markdown", script, StringComparison.Ordinal);
Assert.Contains(".toastui-editor-mode-switch", css, StringComparison.Ordinal);
Assert.Contains("display: none", css, StringComparison.Ordinal);
```

- [ ] **Step 2: Verify RED**

Run `dotnet test -c Release --filter FullyQualifiedName~EditorHostAssetTests`.

Expected: failures for the missing bridge APIs and language registration.

- [ ] **Step 3: Implement editor bridge and localization**

Before editor creation, register `zh-CN` using `window.toastui.Editor.setLanguage`. Translate the bundled labels for headings, bold, italic, strike, code, line, quote, lists, task, indent, links, table, image, file/URL fields, row/column actions, alignment, cancel, OK, write, preview, and mode names. Keep the key/value `Markdown: "Markdown"`; use `WYSIWYG: "所见即所得"`.

Initialize with:

```javascript
hideModeSwitch: true,
language: "zh-CN",
```

Add:

```javascript
function executeHistoryCommand(command) {
  if (!editor || typeof editor.exec !== "function") return false;
  editor.exec(command);
  postMarkdownChanged();
  return true;
}

function handleEditorKeyDown(event) {
  if ((event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === "y") {
    event.preventDefault();
    event.stopPropagation();
    executeHistoryCommand("redo");
  }
}
```

Register the keydown handler in capture phase. Expose `undo`, `redo`, `toggleMode`, and `focus` from `window.qingjianEditor`. `toggleMode` changes to the opposite mode and returns it. Change generated image alt text from `image` to `图片`.

In CSS add:

```css
.toastui-editor-mode-switch {
  display: none;
}
```

- [ ] **Step 4: Verify GREEN and commit**

Run `dotnet test -c Release --filter FullyQualifiedName~EditorHostAssetTests`.

```powershell
git add src/QingJian.App/EditorAssets/editor-host.js src/QingJian.App/EditorAssets/editor-host.css tests/QingJian.App.Tests/Editor/EditorHostAssetTests.cs
git commit -m "feat: add editor history and mode bridge"
```

### Task 4: Build the Grouped Navigation and Icon Action UI

**Files:**
- Modify: `src/QingJian.App/Resources/Styles.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`

**Interfaces:**
- Consumes: `NotesView`, `NoteCreatedAtDisplayConverter`, and editor click handlers implemented in Task 5.
- Produces: grouped navigation headers, creation footer, and icon buttons named `ModeToggleButton`, `UndoButton`, `RedoButton`, and `DeleteButton`.

- [ ] **Step 1: Write failing XAML contract tests**

Assert:

1. The note ListBox binds `ItemsSource="{Binding NotesView}"` and contains a `GroupStyle.HeaderTemplate`.
2. `TitleTextBox` has `PreviewKeyDown="TitleTextBox_OnPreviewKeyDown"`.
3. The four named buttons appear in mode/undo/redo/delete document order.
4. Buttons after the first use `Margin="8,0,0,0"`.
5. Every icon button uses `ToolTipService.InitialShowDelay="0"`.
6. The delete button uses `DangerIconButtonStyle` and `Click="DeleteButton_OnClick"`, with no direct delete command binding.
7. The editor shell contains `CreatedAtTextBlock` binding through `NoteCreatedAtDisplayConverter`.
8. A `NoteCreatedAtDisplayConverter` resource exists.

- [ ] **Step 2: Verify RED**

Run `dotnet test -c Release --filter FullyQualifiedName~MainWindowXamlTests`.

Expected: failures for missing grouping, icon buttons, footer, and Tab handler.

- [ ] **Step 3: Add reusable icon styles**

Add `IconButtonStyle` based on `RoundedButtonStyle` with width/height `32`, `MinHeight="0"`, `Padding="0"`, transparent background/border, and neutral foreground. Add `DangerIconButtonStyle` based on it with `DangerTextBrush` foreground.

- [ ] **Step 4: Update MainWindow XAML**

Add `xmlns:views` and a window resource:

```xml
<views:NoteCreatedAtDisplayConverter x:Key="NoteCreatedAtDisplayConverter" />
```

Bind the list to `NotesView` and add a muted semibold group header with top spacing. Add `PreviewKeyDown` to the title. Replace the single text delete button with a horizontal action stack containing the four named 32 DIP icon buttons. Use Segoe MDL2 Assets glyphs `&#xE8AB;`, `&#xE7A7;`, `&#xE7A6;`, and `&#xE74D;`.

Give undo/redo/delete static Chinese tooltips. Bind mode tooltip to the button's code-behind-updated `ToolTip`. Add 8 DIP margins after the first icon.

Inside `EditorShell`, use rows `*` and `Auto`; place WebView/fallback in row 0 and add:

```xml
<Border Grid.Row="1" BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,1,0,0" Padding="12,7">
    <TextBlock x:Name="CreatedAtTextBlock"
               Text="{Binding SelectedNote.CreatedAt, Converter={StaticResource NoteCreatedAtDisplayConverter}}"
               Foreground="{StaticResource MutedTextBrush}"
               FontSize="11" />
</Border>
```

- [ ] **Step 5: Verify GREEN and commit**

Run `dotnet test -c Release --filter FullyQualifiedName~MainWindowXamlTests`.

```powershell
git add src/QingJian.App/Resources/Styles.xaml src/QingJian.App/Views/MainWindow.xaml tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs
git commit -m "feat: add grouped navigation and editor actions"
```

### Task 5: Wire Confirmation, Focus, Commands, and Chinese Errors

**Files:**
- Create: `src/QingJian.App/Views/NoteDeletionConfirmation.cs`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Create: `tests/QingJian.App.Tests/Views/NoteDeletionConfirmationTests.cs`
- Create: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: named XAML controls and `window.qingjianEditor` APIs.
- Produces: `DeleteButton_OnClick`, `UndoButton_OnClick`, `RedoButton_OnClick`, `ModeToggleButton_OnClick`, and `TitleTextBox_OnPreviewKeyDown`.

- [ ] **Step 1: Write failing confirmation and source-contract tests**

Test:

```csharp
Assert.Equal("确定要删除便签“测试标题”吗？", NoteDeletionConfirmation.BuildPrompt("测试标题"));
Assert.True(NoteDeletionConfirmation.IsConfirmed(MessageBoxResult.Yes));
Assert.False(NoteDeletionConfirmation.IsConfirmed(MessageBoxResult.No));
```

Read `MainWindow.xaml.cs` and assert it contains the four click handlers, `window.qingjianEditor.undo()`, `redo()`, `toggleMode()`, `focus()`, `RefreshNoteNavigation()`, and Chinese close-save text `关闭前无法保存当前便签`.

- [ ] **Step 2: Verify RED**

Run `dotnet test -c Release --filter "FullyQualifiedName~NoteDeletionConfirmationTests|FullyQualifiedName~MainWindowCodeBehindTests"`.

Expected: compile/source failures because helper and handlers do not exist.

- [ ] **Step 3: Implement confirmation and editor commands**

`NoteDeletionConfirmation` provides `BuildPrompt` and `IsConfirmed` exactly as tested.

In `MainWindow`:

1. Subscribe `Activated` to call `_viewModel.RefreshNoteNavigation()`.
2. Update the mode tooltip after settings load and after `EditorModeChangedType` messages.
3. Implement a guarded `ExecuteEditorCommandAsync(string script)` that returns when the editor is not ready and catches WebView command exceptions.
4. Undo/redo/mode handlers call the matching bridge scripts.
5. Delete handler captures the selected note, shows owned Yes/No warning with default No, rechecks selection, and executes the existing command only on Yes.
6. `TitleTextBox_OnPreviewKeyDown` handles forward Tab, updates the title source, sets `e.Handled = true`, focuses `MarkdownWebView`, and invokes `focus()`.
7. Replace the English close-save error with `关闭前无法保存当前便签。\n\n{ex.Message}`.

`UpdateModeToggleToolTip` uses:

```csharp
ModeToggleButton.ToolTip = _currentEditorMode == "markdown"
    ? "切换到所见即所得"
    : "切换到 Markdown";
```

- [ ] **Step 4: Verify GREEN and commit**

Run the focused tests from Step 2, then `dotnet test -c Release --filter FullyQualifiedName~EditorMessageTests`.

```powershell
git add src/QingJian.App/Views/NoteDeletionConfirmation.cs src/QingJian.App/Views/MainWindow.xaml.cs tests/QingJian.App.Tests/Views/NoteDeletionConfirmationTests.cs tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs
git commit -m "feat: wire protected editor workflow actions"
```

### Task 6: Full Verification and Manual Workflow Check

**Files:**
- Modify only if verification reveals a regression directly caused by Tasks 1-5.

**Interfaces:**
- Consumes: completed workflow polish.
- Produces: fresh test, build, and runtime evidence on `feature/ui-polish`.

- [ ] **Step 1: Run all automated tests serially**

Run `dotnet test -c Release`.

Expected: all tests pass with 0 failures and 0 skipped tests.

- [ ] **Step 2: Run the Release build**

Run `dotnet build -c Release --no-restore`.

Expected: 0 warnings and 0 errors.

- [ ] **Step 3: Launch the Release app and inspect**

Run `src\QingJian.App\bin\Release\net8.0-windows\QingJian.App.exe`. If another QingJian instance owns the hotkey, dismiss only the new test instance's registration warning.

Verify:

1. Group headers and descending note order.
2. Delete No leaves the note intact; Delete Yes removes it.
3. Title Tab enters the body editor.
4. Undo, redo, `Ctrl+Y`, and mode switching work in Markdown and 所见即所得.
5. Tooltips appear immediately and contain Chinese copy.
6. Creation date replaces the old mode switch strip.
7. Current rounded styling, desktop todo toggle, quick notes, and editor content remain intact.

- [ ] **Step 4: Review branch scope**

Run:

```powershell
git diff --check ba51b89..HEAD
git diff --name-status ba51b89..HEAD
git status --short --branch
git log --oneline --decorate -10
```

Expected: branch is `feature/ui-polish`, worktree is clean, and `develop` remains at `ba51b89`.
