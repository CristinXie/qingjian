# QingJian Batch Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan inline and task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an in-place, fixed-scope batch mode to the main note navigation with atomic move, favorite, unfavorite, and soft-delete operations.

**Architecture:** WPF `ListBox` multiple selection remains the UI source of truth while `MainViewModel` owns the captured scope, selected notes, command state, and post-operation navigation. New EF Core repository methods perform one `ExecuteUpdateAsync` per action; `NoteService` validates and updates in-memory notes only after persistence succeeds.

**Tech Stack:** .NET 8, WPF, EF Core 8, SQLite, xUnit, WebView2, Segoe MDL2 Assets.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`; leave its pre-existing untracked `outputs/` untouched.
- Follow `docs/superpowers/specs/2026-07-26-batch-management-design.md` exactly.
- Batch move/favorite/unfavorite must not modify `UpdatedAt`.
- Icon buttons must be borderless/backgroundless with immediate Chinese tooltips and 8 DIP spacing.
- Keep all user-facing text Chinese except the product term `Markdown`.
- Use TDD and commit each independently testable task.

---

### Task 1: Atomic Batch Repository Operations

**Files:**
- Modify: `src/QingJian.App/Data/INoteRepository.cs`
- Modify: `src/QingJian.App/Data/NoteRepository.cs`
- Modify: `tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs`
- Modify: `tests/QingJian.App.Tests/Services/NoteServiceTests.cs` only to keep its fake repository compiling

**Interfaces:**
- Produces: `SetFavoritesAsync`, `MoveToFolderAsync`, and collection overload of `SoftDeleteAsync` for Task 2.

- [ ] **Step 1: Add failing SQLite repository tests**

Cover duplicate IDs, unknown IDs, empty collections, and field isolation. The core assertions must prove that favorite/move leave `UpdatedAt` unchanged and deletion uses one supplied timestamp:

```csharp
await repository.SetFavoritesAsync(new[] { "a", "a", "b" }, true, favoriteTime);
await repository.MoveToFolderAsync(new[] { "a", "b" }, "工作");
await repository.SoftDeleteAsync(new[] { "a", "b" }, deletedAt);

Assert.Equal(originalUpdatedAt, reloadedFavorite.UpdatedAt);
Assert.Equal("工作", reloadedMoved.FolderName);
Assert.Equal(deletedAt, reloadedDeleted.UpdatedAt);
```

- [ ] **Step 2: Run repository tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~NoteRepositoryTests
```

Expected: compile failure because the batch methods do not exist.

- [ ] **Step 3: Extend the repository contract**

Add these exact signatures without removing single-note APIs:

```csharp
Task SetFavoritesAsync(
    IReadOnlyCollection<string> noteIds,
    bool isFavorite,
    DateTime? favoritedAt,
    CancellationToken cancellationToken = default);

Task MoveToFolderAsync(
    IReadOnlyCollection<string> noteIds,
    string folderName,
    CancellationToken cancellationToken = default);

Task SoftDeleteAsync(
    IReadOnlyCollection<string> noteIds,
    DateTime deletedAt,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement one-statement EF Core updates**

Normalize IDs with `Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray()`. Return immediately for an empty array. Use `ExecuteUpdateAsync`:

```csharp
await _dbContext.Notes
    .Where(note => ids.Contains(note.Id))
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(note => note.IsFavorite, isFavorite)
        .SetProperty(note => note.FavoritedAt, favoritedAt), cancellationToken);
```

Move sets only `FolderName`. Soft delete sets only `IsDeleted` and `UpdatedAt`. Do not loop through `SaveChangesAsync`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all `NoteRepositoryTests` pass.

- [ ] **Step 6: Commit repository operations**

```powershell
git add src/QingJian.App/Data tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs tests/QingJian.App.Tests/Services/NoteServiceTests.cs
git commit -m "feat: add atomic batch note updates"
```

---

### Task 2: Batch Note Service Semantics

**Files:**
- Modify: `src/QingJian.App/Services/INoteService.cs`
- Modify: `src/QingJian.App/Services/NoteService.cs`
- Modify: `tests/QingJian.App.Tests/Services/NoteServiceTests.cs`
- Modify: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`
- Modify: `tests/QingJian.App.Tests/QuickNotes/QuickNoteCoordinatorTests.cs`

**Interfaces:**
- Consumes: Task 1 repository APIs and existing `IFolderService` validation.
- Produces: batch service methods consumed by `MainViewModel` in Task 3.

- [ ] **Step 1: Add failing service tests**

Test these exact behaviors:

```csharp
await service.SetFavoritesAsync(new[] { normal, existingFavorite }, true);
Assert.Equal(now, normal.FavoritedAt);
Assert.Equal(existingFavoriteTime, existingFavorite.FavoritedAt);
Assert.Equal(originalUpdatedAt, normal.UpdatedAt);

await service.MoveNotesAsync(new[] { first, second }, "工作");
Assert.All(new[] { first, second }, note => Assert.Equal("工作", note.FolderName));
```

Also make the fake repository throw and assert every in-memory note remains unchanged. Verify all-no-op collections do not call persistence.

- [ ] **Step 2: Run service tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~NoteServiceTests
```

Expected: compile failure for missing batch service methods.

- [ ] **Step 3: Extend `INoteService`**

```csharp
Task SetFavoritesAsync(
    IReadOnlyCollection<Note> notes,
    bool isFavorite,
    CancellationToken cancellationToken = default);

Task MoveNotesAsync(
    IReadOnlyCollection<Note> notes,
    string folderName,
    CancellationToken cancellationToken = default);

Task DeleteNotesAsync(
    IReadOnlyCollection<Note> notes,
    CancellationToken cancellationToken = default);
```

Update both in-memory `INoteService` fakes with behaviorally correct implementations, not `NotImplementedException`, so integration tests can exercise batch flows.

- [ ] **Step 4: Implement filtered, persistence-first mutations**

Deduplicate notes by `Id`. Favorite only notes whose state changes; preserve existing favorite timestamps. Move only notes not already in the normalized target. Delete all unique notes. Generate one `_utcNow()` value per favorite or delete action. Call the repository first, then update in-memory properties.

- [ ] **Step 5: Run service and compile-surface tests**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~NoteServiceTests|FullyQualifiedName~QuickNoteCoordinatorTests"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit service behavior**

```powershell
git add src/QingJian.App/Services tests/QingJian.App.Tests/Services/NoteServiceTests.cs tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs tests/QingJian.App.Tests/QuickNotes/QuickNoteCoordinatorTests.cs
git commit -m "feat: add batch note service operations"
```

---

### Task 3: Fixed-Scope Batch State in MainViewModel

**Files:**
- Create: `src/QingJian.App/ViewModels/BatchSelectionState.cs`
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`
- Create: `tests/QingJian.App.Tests/ViewModels/BatchSelectionStateTests.cs`
- Modify: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Consumes: Task 2 service APIs and existing `NotesView`/folder refresh behavior.
- Produces: batch state and operation methods consumed by MainWindow.

- [ ] **Step 1: Write failing state tests**

`BatchSelectionStateTests` must cover fixed-scope capture, deduplicated selection, out-of-scope rejection, count, all-favorite flags, and clear/exit. `MainViewModelTests` must cover:

```csharp
viewModel.SearchText = "alpha";
viewModel.EnterBatchMode();
viewModel.AddSavedNote(newQuickNote, select: false);

Assert.True(viewModel.IsBatchMode);
Assert.DoesNotContain(newQuickNote, viewModel.NotesView.Cast<Note>());
Assert.Equal(expectedVisibleIds, viewModel.BatchScopeIds);
```

Add cases for favorite reordering, moving out of the current folder, deleting the normal selected note, success clearing selection, and failure preserving selection.

- [ ] **Step 2: Run ViewModel tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~BatchSelectionStateTests|FullyQualifiedName~MainViewModelTests"
```

Expected: compile failure because batch state is missing.

- [ ] **Step 3: Implement focused selection state**

`BatchSelectionState` owns `HashSet<string>` scope IDs and selected IDs. It exposes:

```csharp
public IReadOnlySet<string> ScopeIds { get; }
public IReadOnlyList<Note> SelectedNotes { get; }
public int SelectedCount { get; }
public bool AllSelectedAreFavorite { get; }
public bool AllSelectedAreNotFavorite { get; }
public void Enter(IEnumerable<Note> visibleNotes);
public void SetSelection(IEnumerable<Note> selectedNotes);
public void ClearSelection();
public void Exit();
```

Keep it independent of WPF types so tests do not require a dispatcher.

- [ ] **Step 4: Integrate mode and command state into MainViewModel**

Expose:

```csharp
public bool IsBatchMode { get; }
public IReadOnlySet<string> BatchScopeIds { get; }
public int BatchSelectedCount { get; }
public bool HasBatchSelection { get; }
public bool CanEnterBatchMode { get; }
public bool CanBatchFavorite { get; }
public bool CanBatchUnfavorite { get; }

public void EnterBatchMode();
public void ExitBatchMode();
public void SetBatchSelection(IEnumerable<Note> notes);
public Task SetBatchFavoriteAsync(bool isFavorite, CancellationToken cancellationToken = default);
public Task MoveBatchSelectionAsync(string folderName, CancellationToken cancellationToken = default);
public Task DeleteBatchSelectionAsync(CancellationToken cancellationToken = default);
```

`FilterNote` must reject notes outside `BatchScopeIds` while batch mode is active. Successful operations clear selection; failures do not. Raise all dependent properties from one `NotifyBatchStateChanged` helper.

- [ ] **Step 5: Implement post-operation navigation**

Before move/delete, capture `SelectedNote`'s index in `NotesView`. After refresh, preserve it if visible; otherwise select `remaining[Math.Min(oldIndex, remaining.Count - 1)]`. Refresh folder counts after move/delete. Do not move items in the backing collection for favorite operations; let the existing comparer reorder the view.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all batch-state and existing MainViewModel tests pass.

- [ ] **Step 7: Commit batch navigation state**

```powershell
git add src/QingJian.App/ViewModels tests/QingJian.App.Tests/ViewModels
git commit -m "feat: add fixed-scope batch selection state"
```

---

### Task 4: In-Place Multi-Select Main Window UI

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Resources/Styles.xaml` only if the existing list item style cannot express selected checkbox spacing
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`

**Interfaces:**
- Consumes: Task 3 bindable properties.
- Produces: named controls and event hooks for Task 5.

- [ ] **Step 1: Add failing XAML structure tests**

Assert exact named controls:

```text
NoteListBox
BatchSelectionCheckBox
NormalSidebarFooter
BatchActionBar
BatchSelectedCountTextBlock
BatchSelectAllButton
BatchClearSelectionButton
BatchExitButton
BatchMoveButton
BatchFavoriteButton
BatchUnfavoriteButton
BatchDeleteButton
EditorInteractionPanel
```

Verify `BatchActionBar` visibility binds to `IsBatchMode`, every icon uses `IconButtonStyle`, every tooltip delay is `0`, and horizontal action margins are exactly `8,0,0,0`.

- [ ] **Step 2: Run XAML tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~MainWindowXamlTests
```

Expected: failures for missing batch controls.

- [ ] **Step 3: Convert the list to mode-dependent selection**

Name the list `NoteListBox`. Move its current `SelectedItem` binding into a `ListBox.Style`: normal mode uses `Single` plus `SelectedNote`; batch mode uses `Multiple`. Add a checkbox bound to the ancestor `ListBoxItem.IsSelected` and visible only in batch mode. Hide the favorite star in the same state and disable card folder assignment.

- [ ] **Step 4: Add the two-row action bar**

Use exact tooltips:

```text
全选当前结果
清空选择
退出批量管理
移动到文件夹
批量收藏
批量取消收藏
批量删除
```

Bind the count with `StringFormat=已选择 {0} 项`. Bind write action enabled states to `HasBatchSelection`, `CanBatchFavorite`, and `CanBatchUnfavorite`. Keep stable 32 DIP icon button dimensions and 8 DIP spacing.

- [ ] **Step 5: Lock conflicting controls during batch mode**

Use style data triggers, not a new converter, to disable new note, search, sort, clear-folder-filter, title, editor toolbar and WebView hit testing while `IsBatchMode=true`. Hide `NormalSidebarFooter`. Do not show explanatory overlay text.

- [ ] **Step 6: Run XAML tests and verify GREEN**

Run the command from Step 2. Expected: all main-window XAML tests pass, including existing three-row card spacing tests.

- [ ] **Step 7: Commit the batch UI**

```powershell
git add src/QingJian.App/Views/MainWindow.xaml src/QingJian.App/Resources/Styles.xaml tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs
git commit -m "feat: add in-place batch selection UI"
```

---

### Task 5: MainWindow Orchestration, Menus, and Keyboard Actions

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/NoteDeletionConfirmationTests.cs`
- Create: `src/QingJian.App/Views/BatchNoteDeletionConfirmation.cs`
- Create: `tests/QingJian.App.Tests/Views/BatchNoteDeletionConfirmationTests.cs`

**Interfaces:**
- Consumes: Task 3 ViewModel methods and Task 4 named controls.
- Produces: complete entry, selection, move, favorite, delete, exit and keyboard workflows.

- [ ] **Step 1: Add failing orchestration tests**

Source-structure tests must assert handlers for all named buttons and these required calls/text:

```csharp
await PullLatestEditorMarkdownAsync();
await _viewModel.SaveSelectedNoteNowAsync();
_viewModel.SetBatchSelection(NoteListBox.SelectedItems.Cast<Note>());
```

`BatchNoteDeletionConfirmationTests` must verify count insertion, default `No`, and exact warning text.

- [ ] **Step 2: Run view tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowCodeBehindTests|FullyQualifiedName~BatchNoteDeletionConfirmationTests"
```

Expected: failures because handlers and confirmation helper are missing.

- [ ] **Step 3: Implement protected mode entry and exit**

`BatchManagementButton_OnClick` must pull editor Markdown, save immediately, then call `EnterBatchMode` and clear WPF selection. On failure, remain in normal mode and show `进入批量管理前无法保存当前便签。`. Exit clears WPF selection, calls `ExitBatchMode`, and restores normal `SelectedNote` selection without reloading stale editor content.

- [ ] **Step 4: Synchronize WPF multiple selection**

Branch `NoteListBox_OnSelectionChanged`: in batch mode call `SetBatchSelection` and never assign `SelectedNote`; in normal mode preserve existing behavior. Select-all and clear buttons call `NoteListBox.SelectAll()` / `UnselectAll()` and then synchronize.

- [ ] **Step 5: Implement batch favorite actions**

Call `_viewModel.SetBatchFavoriteAsync(true/false)`. On success unselect all. On exception retain selected items and show `批量收藏失败。` or `批量取消收藏失败。` followed by the concrete message.

- [ ] **Step 6: Implement reusable folder move menu**

Extract only the menu population shared by single-note and batch move paths. Batch move uses the current selected snapshot, disables a target shared by every selected note, and retains `新建文件夹…`. New-folder success refreshes folders and then calls `MoveBatchSelectionAsync`. Do not duplicate folder validation in MainWindow.

- [ ] **Step 7: Implement protected batch deletion**

`BatchNoteDeletionConfirmation.BuildPrompt(count)` returns:

```text
确定删除选中的 X 条便签吗？

删除后暂时无法在应用内恢复。
```

Use `MessageBoxButton.YesNo`, `MessageBoxResult.No`, then call `DeleteBatchSelectionAsync`. Failure keeps selection and shows `批量删除便签失败。`.

- [ ] **Step 8: Add keyboard handling**

At window preview-key level, only when `IsBatchMode` is true: `Ctrl+A` selects all, `Delete` uses the same delete path, and `Esc` exits. Set `e.Handled=true`. Preserve all normal editor shortcuts outside batch mode.

- [ ] **Step 9: Run view tests and verify GREEN**

Run the command from Step 2, then run all `MainWindow` tests. Expected: all pass.

- [ ] **Step 10: Commit complete interaction wiring**

```powershell
git add src/QingJian.App/Views tests/QingJian.App.Tests/Views
git commit -m "feat: wire batch note actions"
```

---

### Task 6: Cross-Layer Regression Coverage

**Files:**
- Modify only the focused files from Tasks 1-5 where a genuine missing edge-case test is found.
- Do not modify `docs/project-status.md` before branch completion and merge.

**Interfaces:**
- Consumes: the complete batch feature.
- Produces: verified cross-layer behavior before runtime testing.

- [ ] **Step 1: Run all batch-focused tests**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~NoteRepositoryTests|FullyQualifiedName~NoteServiceTests|FullyQualifiedName~BatchSelectionStateTests|FullyQualifiedName~MainViewModelTests|FullyQualifiedName~MainWindow|FullyQualifiedName~BatchNoteDeletionConfirmationTests"
```

Expected: all selected tests pass.

- [ ] **Step 2: Run the full Release suite serially**

```powershell
dotnet test -c Release
```

Expected: 0 failed and 0 skipped. Record the exact passed total.

- [ ] **Step 3: Build Release without restore**

```powershell
dotnet build -c Release --no-restore
```

Expected: 0 warnings and 0 errors.

- [ ] **Step 4: Review regression invariants**

Confirm tests prove single-note delete/favorite/move, quick-note creation, editor autosave, folder filtering, search ranking and both navigation sort modes remain unchanged.

- [ ] **Step 5: Commit only genuine regression corrections**

If this task changes tests or implementation, commit exact files with:

```powershell
git commit -m "test: cover batch note workflows"
```

Do not create an empty commit.

---

### Task 7: Runtime UI and Worktree Verification

**Files:**
- Modify no files unless runtime verification exposes a reproducible defect with a new failing test.

**Interfaces:**
- Consumes: complete implementation.
- Produces: user-visible acceptance evidence.

- [ ] **Step 1: Start from the feature worktree**

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj -c Release --no-build
```

Expected: main window opens without startup errors.

- [ ] **Step 2: Verify entry and fixed scope**

Edit a body, immediately enter batch mode, then exit and confirm the edit persisted. Re-enter with a search and folder filter active; verify only those visible cards are selectable. Save a quick note from the hotkey while batch mode is active; it must appear only after exit.

- [ ] **Step 3: Verify selection and controls**

Check card click, checkbox click, select all, clear, `Ctrl+A`, and `Esc`. Confirm count accuracy, disabled zero-selection actions, favorite/unfavorite availability, editor lock, hidden normal footer, Chinese tooltips, borderless icon buttons and 8 DIP spacing.

- [ ] **Step 4: Verify favorite operations**

Select mixed favorite states. Batch favorite and batch unfavorite; verify only required states change, selection clears, favorite sorting updates correctly, and modification times do not change.

- [ ] **Step 5: Verify move operations**

Move mixed-folder notes to an existing folder, then create a folder from the move menu. Verify folder labels/counts, current-folder disappearance, all-notes retention, nearest selected-note fallback and unchanged modification times.

- [ ] **Step 6: Verify deletion and failure safety**

Cancel one delete confirmation, then confirm another. Check exact count text, default “否”, removal from list, folder counts and nearest selection. Exercise an invalid/removed target folder and confirm the batch selection remains intact with a Chinese error.

- [ ] **Step 7: Inspect normal and narrow layouts**

Verify no overlap or clipping in long titles/folder names, selected backgrounds, checkboxes and the two-row action bar at normal and minimum window sizes.

- [ ] **Step 8: Stop the app and verify isolation**

```powershell
Get-Process QingJian.App -ErrorAction SilentlyContinue | Stop-Process
git status --short --branch
git -C C:\Users\Cristin\Desktop\VibeCoding\qingjian status --short --branch
```

Expected: no app process remains; implementation exists only on `feature/ui-polish`; `develop` remains at its prior commit with pre-existing `outputs/` untouched.

---

## Completion Gate

Implementation is complete only when:

1. Every behavior in `docs/superpowers/specs/2026-07-26-batch-management-design.md` is covered.
2. Batch repository updates are atomic and do not change unrelated columns.
3. Failed operations preserve database, in-memory notes, fixed scope and WPF selection.
4. `dotnet test -c Release` passes with 0 failures and 0 skipped.
5. `dotnet build -c Release --no-restore` reports 0 warnings and 0 errors.
6. Runtime checks cover fixed scope, all four operations, keyboard controls, narrow layout and editor-save protection.
7. `develop` has not moved or been modified.

Do not merge. Merge and update `develop` documentation only after the user says `完成分支收尾工作`.
