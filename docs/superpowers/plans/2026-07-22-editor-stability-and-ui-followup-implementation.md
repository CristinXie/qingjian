# QingJian Editor Stability and UI Follow-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent autosave navigation refreshes from reloading the active Markdown editor, balance note-card spacing, show the active sort-mode icon, and remove white input surfaces from todo popovers.

**Architecture:** Add a pure same-note reload guard to `MarkdownEditorState` and consume it from `MainWindow` before loading selection content. Keep layout changes in existing XAML resources and verify them with source/XAML contract tests.

**Tech Stack:** .NET 8, WPF/XAML, WebView2, Toast UI Editor, xUnit, XML/source contract tests.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Run all .NET test and build commands serially with `-c Release`.
- Preserve one-way navigation selection restoration, autosave timing, editor undo history, and Markdown storage.
- Preserve todo validation, border visibility, scrolling, fixed footers, and text-button styling.

---

### Task 1: Stop Same-Note Autosave Reloads

**Files:**
- Modify: `src/QingJian.App/Editor/MarkdownEditorState.cs`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/Editor/MarkdownEditorStateTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- Produces: `MarkdownEditorState.ShouldReloadSelection(string? noteId)`.
- Preserves: `BeginLoad`, stale-message filtering, and one-way list selection restoration.

- [ ] **Step 1: Write failing reload-guard tests**

Add tests proving a repeated note ID returns false and a different note ID returns true:

```csharp
[Fact]
public void ShouldReloadSelection_ReturnsFalseForCurrentNote()
{
    var state = new MarkdownEditorState();
    state.BeginLoad("note-1", "Body");
    state.EndLoad();

    Assert.False(state.ShouldReloadSelection("note-1"));
}

[Fact]
public void ShouldReloadSelection_ReturnsTrueForDifferentNote()
{
    var state = new MarkdownEditorState();
    state.BeginLoad("note-1", "Body");
    state.EndLoad();

    Assert.True(state.ShouldReloadSelection("note-2"));
}
```

Extend `MainWindowCodeBehindTests` to require `ShouldReloadSelection` in the `SelectedNote` notification path.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter "FullyQualifiedName~MarkdownEditorStateTests|FullyQualifiedName~MainWindowCodeBehindTests"
```

Expected: compile failure because `ShouldReloadSelection` does not exist.

- [ ] **Step 3: Implement the pure guard and guarded load helper**

Add:

```csharp
public bool ShouldReloadSelection(string? noteId)
{
    return !string.Equals(CurrentNoteId, noteId, StringComparison.Ordinal);
}
```

In `MainWindow`, route selection notifications and initial post-load synchronization through:

```csharp
private Task LoadSelectedNoteIfChangedAsync()
{
    return _editorState.ShouldReloadSelection(_viewModel.SelectedNote?.Id)
        ? LoadSelectedNoteIntoEditorAsync()
        : Task.CompletedTask;
}
```

Do not call `LoadSelectedNoteIntoEditorAsync` directly from repeated `SelectedNote` notifications.

- [ ] **Step 4: Verify GREEN and commit**

Run the focused test command from Step 2, then commit:

```powershell
git add src/QingJian.App/Editor/MarkdownEditorState.cs src/QingJian.App/Views/MainWindow.xaml.cs tests/QingJian.App.Tests/Editor/MarkdownEditorStateTests.cs tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs
git commit -m "fix: preserve editor cursor across autosave"
```

---

### Task 2: Balance Note Cards and Show Active Sort Icon

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Resources/Styles.xaml`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`

**Interfaces:**
- Produces: three 24 DIP preview rows and active-mode sort glyphs.
- Preserves: next-action tooltips, favorite behavior, and 8 DIP card corners.

- [ ] **Step 1: Write failing XAML contract tests**

Require note preview row heights `24,24,24`, centered row content, `FavoriteButton` size `24x24`, no row-2/row-3 top margins, and `NoteListBoxItemStyle` padding `12,8`.

Require the sort button default content to be clock `\uE823`, with a `Favorite` trigger that sets star `\uE734`; keep default tooltip `按收藏` and favorite-mode tooltip `按时间`.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter FullyQualifiedName~MainWindowXamlTests
```

Expected: failures for automatic row heights, existing margins, 32 DIP favorite button inheritance, and reversed sort glyphs.

- [ ] **Step 3: Implement balanced layout and glyph semantics**

Set `NoteListBoxItemStyle` padding to `12,8`. Use three `<RowDefinition Height="24" />` rows, center the updated-time and folder row content vertically, remove `Margin="0,4,0,0"` and `Margin="0,5,0,0"`, and set `FavoriteButton` width/height/min-height to 24.

Set the sort button's default glyph to `&#xE823;` and the `Favorite` trigger glyph to `&#xE734;`. Do not swap the tooltip values.

- [ ] **Step 4: Verify GREEN and commit**

Run the focused test command from Step 2, then commit:

```powershell
git add src/QingJian.App/Views/MainWindow.xaml src/QingJian.App/Resources/Styles.xaml tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs
git commit -m "style: balance note previews and sort status"
```

---

### Task 3: Match Todo Input Surfaces to Popover Background

**Files:**
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs`

**Interfaces:**
- Consumes: `TodoWidgetPopoverBackgroundBrush`.
- Produces: matching quick-add/edit body and time input backgrounds.

- [ ] **Step 1: Write failing surface-color tests**

Assert these named elements use `{StaticResource TodoWidgetPopoverBackgroundBrush}`:

```text
TodoTextBox
EditTimePickerBorder
EditStartHourTextBox
EditStartMinuteTextBox
EditEndHourTextBox
EditEndMinuteTextBox
QuickAddTextBox
QuickAddTimePickerBorder
QuickAddStartHourTextBox
QuickAddStartMinuteTextBox
QuickAddEndHourTextBox
QuickAddEndMinuteTextBox
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test -c Release --filter FullyQualifiedName~TodoWidgetWindowXamlTests
```

Expected: failures because all listed surfaces currently use `#FFFFFF`.

- [ ] **Step 3: Replace only the listed backgrounds**

Change each listed element to:

```xml
Background="{StaticResource TodoWidgetPopoverBackgroundBrush}"
```

Do not change borders, validation brushes, or text buttons.

- [ ] **Step 4: Verify GREEN and commit**

Run the focused test command from Step 2, then commit:

```powershell
git add src/QingJian.App/TodoWidgets/TodoWidgetWindow.xaml tests/QingJian.App.Tests/TodoWidgets/TodoWidgetWindowXamlTests.cs
git commit -m "style: match todo inputs to popover surfaces"
```

---

### Task 4: Full Verification

**Files:**
- Modify only files responsible for a discovered regression.

**Interfaces:**
- Produces: fresh automated, build, runtime, and branch-scope evidence.

- [ ] **Step 1: Run all tests and build serially**

```powershell
dotnet test -c Release
dotnet build -c Release --no-restore
```

Expected: zero failures, zero skipped tests, zero warnings, and zero errors.

- [ ] **Step 2: Runtime-check the editor pause case**

Type multiple characters in the main body editor, pause longer than 700 ms, and verify the cursor does not move and body statistics match the current Markdown. Switch notes afterward and verify the new note still loads.

- [ ] **Step 3: Runtime-check the visual follow-ups**

Verify equal note-card spacing, clock/star active-mode icons, and matching light-green quick-add/edit body and time surfaces.

- [ ] **Step 4: Review branch scope**

```powershell
git diff --check develop..HEAD
git status --short --branch
git rev-parse develop
```

Expected: clean `feature/ui-polish`; `develop` remains `ba51b89cb6738f0dbb059e37bc40248e4bbd24b6`.
