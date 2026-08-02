# QingJian Recycle Bin Time and Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve true note modification times across recycle-bin workflows and add a favorite-free deleted-note preview.

**Architecture:** Track unsaved title/content edit versions in `MainViewModel` so explicit workflow saves only persist real changes. Convert the recycle-bin list to a selectable master-detail view driven by `RecycleBinViewModel.SelectedNote`.

**Tech Stack:** .NET 8, WPF, EF Core 8, SQLite, xUnit.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Use test-first development for every production behavior change.
- Restore and permanent deletion execute immediately.
- Icon buttons keep existing borderless styles and zero-delay Chinese tooltips.

---

### Task 1: Modification-Time Isolation

**Files:**
- Modify: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`
- Modify: `tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs`
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`

**Interfaces:**
- Produces: `SaveSelectedNoteNowAsync` persists only notes with unsaved title/content versions.

- [ ] Add a failing main-view-model test that calls `SaveSelectedNoteNowAsync` without editing and expects no `SaveNoteAsync` call.
- [ ] Add a failing main-view-model test that changes content during an in-flight save and expects a second explicit save to persist the newer version.
- [ ] Add a repository regression test with active and multiple deleted notes, then restore one and permanently delete another while asserting every surviving `UpdatedAt` value is unchanged.
- [ ] Run the focused tests and verify the main-view-model tests fail for the expected unconditional-save behavior.
- [ ] Add per-note edit-version tracking, capture the version before persistence, and clear it only when the captured version remains current after success.
- [ ] Run the focused tests and commit the time-isolation change.

### Task 2: Recycle-Bin Selection and Preview

**Files:**
- Modify: `tests/QingJian.App.Tests/ViewModels/RecycleBinViewModelTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/RecycleBinWindowXamlTests.cs`
- Modify: `src/QingJian.App/ViewModels/RecycleBinViewModel.cs`
- Modify: `src/QingJian.App/Views/RecycleBinWindow.xaml`

**Interfaces:**
- Produces: `Note? RecycleBinViewModel.SelectedNote` for card selection and preview binding.

- [ ] Add failing view-model tests for selecting the first loaded item, choosing the adjacent item after removing the selection, and clearing selection when empty.
- [ ] Replace favorite assertions with failing XAML assertions that no `IsFavorite` binding or star glyph exists.
- [ ] Add failing XAML assertions for a `ListBox` selected-item binding plus read-only `SelectedNote.Title` and `SelectedNote.Content` preview controls.
- [ ] Run the focused tests and verify failures are caused by missing selection and preview behavior.
- [ ] Implement `SelectedNote` and index-preserving removal selection in the view model.
- [ ] Replace the `ItemsControl` with a card-styled `ListBox` and add the read-only detail pane.
- [ ] Run focused tests and commit the preview change.

### Task 3: Verification

**Files:**
- Modify only files required by a reproducible regression.

- [ ] Run all recycle-bin, main-view-model, note-service, and note-repository tests.
- [ ] Run `dotnet test -c Release` and require zero failures and zero skipped tests.
- [ ] Run `dotnet build -c Release --no-restore` and require zero warnings and zero errors.
- [ ] Inspect `git diff --check`, branch status, and both worktrees to confirm only `feature/ui-polish` changed.
