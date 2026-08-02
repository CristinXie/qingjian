# QingJian Recycle Bin and Selection Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add lossless 30-day note recovery and correct navigation selection and batch clear-selection visuals.

**Architecture:** Persist deletion time separately from modification time, expose repository/service lifecycle operations, and use a dedicated recycle-bin view model and modal window. Keep the main navigation authoritative for active notes and refresh it after recycle-bin changes.

**Tech Stack:** .NET 8, WPF, EF Core 8, SQLite, xUnit, Segoe MDL2 Assets.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Use test-first development for every behavior change.
- Icon buttons use existing borderless/backgroundless styles, zero-delay Chinese tooltips, and 8 DIP spacing.
- Restore and permanent deletion execute immediately without confirmation.
- Preserve all note properties across deletion and restoration.

---

### Task 1: Direct Navigation Selection Binding

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- Produces: reliable `SelectedNote` to `NoteListBox.SelectedItem` synchronization.

- [ ] Add failing XAML tests requiring a direct `SelectedItem="{Binding SelectedNote, Mode=OneWay}"` attribute and forbidding a style Setter for `SelectedItem`.
- [ ] Add a failing code-behind test requiring selected-note synchronization and `NoteListBox.ScrollIntoView`.
- [ ] Run the focused view tests and verify failure.
- [ ] Move the binding to the `ListBox` and synchronize/scroll from the existing `SelectedNote` property-change handler.
- [ ] Run focused tests and commit as `fix: keep new notes selected in navigation`.

---

### Task 2: Lossless Deleted-Note Persistence

**Files:**
- Modify: `src/QingJian.App/Models/Note.cs`
- Modify: `src/QingJian.App/Data/AppDbContext.cs`
- Modify: `src/QingJian.App/Data/INoteRepository.cs`
- Modify: `src/QingJian.App/Data/NoteRepository.cs`
- Modify: `src/QingJian.App/Data/FolderRepository.cs`
- Modify: `tests/QingJian.App.Tests/Data/AppDbContextTests.cs`
- Modify: `tests/QingJian.App.Tests/Data/NoteRepositoryTests.cs`
- Modify: `tests/QingJian.App.Tests/Data/FolderRepositoryTests.cs`

**Interfaces:**
- Produces: `GetDeletedNotesAsync(DateTime)`, `RestoreAsync(string)`, and `PermanentlyDeleteAsync(string)`.

- [ ] Add failing SQLite tests for `DeletedAt` migration/backfill and the inclusive 30-day query boundary.
- [ ] Add failing tests proving soft delete leaves `UpdatedAt`, folder, and favorite fields unchanged.
- [ ] Add failing tests proving restore only clears deletion fields and permanent deletion removes the row.
- [ ] Add failing folder tests proving rename/delete leave deleted-note folder snapshots unchanged.
- [ ] Run repository tests and verify failure.
- [ ] Add the model mapping, idempotent migration, query, restore, permanent-delete, and active-only folder updates.
- [ ] Run focused data tests and commit as `feat: persist lossless deleted notes`.

---

### Task 3: Recycle-Bin Service Semantics

**Files:**
- Modify: `src/QingJian.App/Services/INoteService.cs`
- Modify: `src/QingJian.App/Services/NoteService.cs`
- Modify: test fakes implementing `INoteService` and `INoteRepository`
- Modify: `tests/QingJian.App.Tests/Services/NoteServiceTests.cs`

**Interfaces:**
- Produces: `GetRecentlyDeletedNotesAsync`, `RestoreNoteAsync`, and `PermanentlyDeleteNoteAsync`.

- [ ] Add failing service tests for the 30-day cutoff, persistence-first mutation, and exact property preservation.
- [ ] Add failing tests for recreating a missing original folder and leaving `未分类` unchanged.
- [ ] Update interfaces and behaviorally correct test fakes.
- [ ] Implement service methods with repository-first note mutation and folder restoration.
- [ ] Run focused service and compile-surface tests and commit as `feat: add recycle bin note lifecycle`.

---

### Task 4: Recycle-Bin View Model and Window

**Files:**
- Create: `src/QingJian.App/ViewModels/RecycleBinViewModel.cs`
- Create: `src/QingJian.App/Views/RecycleBinWindow.xaml`
- Create: `src/QingJian.App/Views/RecycleBinWindow.xaml.cs`
- Create: `tests/QingJian.App.Tests/ViewModels/RecycleBinViewModelTests.cs`
- Create: `tests/QingJian.App.Tests/Views/RecycleBinWindowXamlTests.cs`
- Create: `tests/QingJian.App.Tests/Views/RecycleBinWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: Task 3 service methods.
- Produces: modal recent-deleted-note list with direct restore and permanent deletion.

- [ ] Add failing view-model tests for loading, successful removal, failed-operation retention, and empty state.
- [ ] Add failing XAML tests for three-line cards, favorite/folder metadata, action icons, Chinese zero-delay tooltips, and 8 DIP spacing.
- [ ] Add failing code-behind tests for direct action handlers and Chinese errors.
- [ ] Implement the view model and modal window using existing resources and converters.
- [ ] Run focused recycle-bin tests and commit as `feat: add recycle bin window`.

---

### Task 5: Main Window Integration and Batch Icon

**Files:**
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `src/QingJian.App/ViewModels/MainViewModel.cs`
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/AppStartupWiringTests.cs`
- Modify: `tests/QingJian.App.Tests/ViewModels/MainViewModelTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: recycle-bin window and lifecycle services.
- Produces: footer entry, post-dialog active-note refresh, and trash glyph for clear selection.

- [ ] Add failing tests requiring footer order `SettingsButton`, `FolderButton`, `BatchManagementButton`, `RecycleBinButton` and exact icon-button conventions.
- [ ] Add failing tests requiring current-editor save before opening and active-note/folder refresh afterward.
- [ ] Add a failing assertion that `BatchClearSelectionButton` uses trash glyph `E74D` while retaining regular icon style and tooltip.
- [ ] Implement app/window wiring and active-note reload preserving the selected note when possible.
- [ ] Run main-window tests and commit as `feat: integrate recycle bin navigation`.

---

### Task 6: Regression and Runtime Verification

**Files:**
- Modify only files needed by a reproducible regression.

- [ ] Stabilize the existing calendar-navigation test by deriving expected months from its injected/current date rather than hard-coded July/August values.
- [ ] Run all recycle-bin, repository, service, view-model, and main-window tests.
- [ ] Run `dotnet test -c Release` and require 0 failures and 0 skipped.
- [ ] Run `dotnet build -c Release --no-restore` and require 0 warnings and 0 errors.
- [ ] Launch the Release app from the feature worktree and verify new-note selection, footer spacing, recycle-bin card layout, direct actions, and batch clear icon at normal and minimum window sizes.
- [ ] Stop the app and verify `feature/ui-polish` is clean and `develop` remains untouched apart from its pre-existing `outputs/`.
