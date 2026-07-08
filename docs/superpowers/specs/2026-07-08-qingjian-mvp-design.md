# QingJian MVP Design

## 1. Product Goal

QingJian is a native Windows sticky-notes application. The first version focuses on a stable local notes experience: users can open the app, create notes, edit notes, delete notes, and have all data restored after restarting the app.

The long-term product direction includes global hotkeys, quick note popups, a transparent desktop todo/schedule widget, tray mode, startup behavior, search, tags, and richer editing. These are intentionally excluded from the MVP so the foundation can be completed quickly and safely.

## 2. MVP Scope

The MVP includes:

1. A complete WPF main window.
2. A left-side note list.
3. A right-side note editor.
4. Creating notes.
5. Editing note title and content.
6. Soft-deleting notes.
7. Automatic local saving.
8. SQLite persistence.
9. Created and updated timestamps.
10. Empty state when there are no notes.
11. Restoring existing notes after app restart.

## 3. Non-Goals

The MVP does not include:

1. Global hotkeys.
2. Quick note popup window.
3. Transparent desktop todo or schedule widget.
4. System tray and background resident mode.
5. Startup on boot.
6. Cloud sync.
7. Rich text editing.
8. Markdown preview.
9. Tags, search, archive, or recycle bin UI.
10. Import/export.

## 4. Technology Stack

The MVP will use:

1. WPF for the native Windows UI.
2. .NET 8 or .NET 9 as the application runtime.
3. SQLite for local persistence.
4. EF Core SQLite for database access and future migration support.
5. MVVM-style structure for separating UI state, business logic, and persistence.

The first version should stay as a single WPF project. Separate class libraries can be introduced later when features such as hotkeys, widgets, and tray mode create clearer module boundaries.

## 5. Project Structure

Initial structure:

```text
QingJian/
  QingJian.sln
  src/
    QingJian.App/
      App.xaml
      Models/
        Note.cs
      Data/
        AppDbContext.cs
        NoteRepository.cs
      Services/
        NoteService.cs
      ViewModels/
        MainViewModel.cs
        NoteEditorViewModel.cs
      Views/
        MainWindow.xaml
      Resources/
        Styles.xaml
```

Responsibilities:

1. `Models` contains simple domain entities such as `Note`.
2. `Data` owns SQLite access, EF Core configuration, and note CRUD operations.
3. `Services` contains app-level note operations and hides persistence details from the UI.
4. `ViewModels` owns screen state, selection state, and user commands.
5. `Views` contains WPF XAML screens and controls.
6. `Resources` contains shared WPF styles.

## 6. Data Model

The MVP uses one main table: `Notes`.

```sql
CREATE TABLE Notes (
  Id TEXT PRIMARY KEY,
  Title TEXT NOT NULL,
  Content TEXT NOT NULL,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  IsDeleted INTEGER NOT NULL DEFAULT 0
);
```

Field rules:

1. `Id` is a GUID string. This leaves room for future sync or import/export.
2. `Title` is required. New notes use a default title such as `未命名便签`.
3. `Content` is required and may be an empty string.
4. `CreatedAt` stores creation time.
5. `UpdatedAt` stores the last edited time.
6. `IsDeleted` supports soft deletion. Deleted notes are hidden from the normal list.

Future tables may include:

```text
Tags
NoteTags
Tasks
Schedules
Settings
```

## 7. Persistence Behavior

Startup behavior:

1. Initialize the SQLite database if it does not exist.
2. Load notes where `IsDeleted = 0`.
3. Sort notes by `UpdatedAt DESC`.
4. Select the most recently updated note if one exists.
5. Show an empty state if there are no notes.

Create behavior:

1. Create a new note immediately.
2. Assign a GUID, default title, empty content, `CreatedAt`, and `UpdatedAt`.
3. Save it to SQLite.
4. Select it in the UI.

Edit behavior:

1. Users edit title and content directly in the right-side editor.
2. Changes are saved automatically.
3. Automatic saving uses debounce behavior, roughly 500-800ms after typing stops.
4. Closing the app triggers a final save for the selected note.

Delete behavior:

1. Deleting a note sets `IsDeleted = 1`.
2. The note disappears from the list.
3. If another note exists, the next available note is selected.
4. If no notes remain, the empty state is shown.

## 8. UI Design

The MVP uses a two-pane layout:

1. Left pane: note list, new-note action, and later room for search.
2. Right pane: selected note title and content editor.

The visual direction should be quiet and work-focused:

1. Compact spacing.
2. Clear selected state in the note list.
3. Readable editor typography.
4. Minimal but polished buttons.
5. Empty state with one clear create-note action.

The MVP should not include a marketing-style landing page. Opening the app should show the usable notes interface immediately.

## 9. Git Workflow

The repository starts with:

```bash
git init
git branch -M main
```

Recommended branches:

```text
main       stable branch with runnable versions
develop    integration branch for ongoing feature work
feature/*  isolated feature branches
fix/*      bug fix branches
release/*  release stabilization branches
```

MVP feature branches:

```text
feature/project-bootstrap
feature/mvp-shell
feature/sqlite-storage
feature/note-list
feature/note-editor
feature/auto-save
feature/delete-note
feature/basic-polish
```

Commit message prefixes:

```text
feat: new user-facing behavior
fix: bug fixes
refactor: internal restructuring without intended behavior change
style: visual or formatting changes
docs: documentation
chore: tooling and configuration
test: tests
```

After the MVP is complete and verified, merge `develop` into `main` and tag `v0.1.0`.

## 10. Roadmap

### v0.1 MVP

1. WPF main window.
2. Left note list.
3. Right note editor.
4. SQLite persistence.
5. Create, edit, delete.
6. Auto-save.
7. Empty state and basic styling.

### v0.2 Quick Notes

1. System tray.
2. Background resident mode.
3. Global hotkey.
4. Quick note popup.
5. Default shortcut such as `Ctrl + Alt + N`.

### v0.3 Desktop Todo/Schedule Widget

1. Transparent desktop window.
2. Always-on-top option.
3. Optional click-through or lock mode.
4. Todo list.
5. Simple schedule list.
6. Separate `Tasks` and `Schedules` tables.
7. Saved opacity, position, and size.

### v0.4 Experience Enhancements

1. Search.
2. Tags.
3. Archive or recycle bin UI.
4. Light and dark themes.
5. Markdown preview or rich text.
6. Import/export.
7. Startup on boot.

## 11. Acceptance Criteria

The MVP is accepted when:

1. The app can be launched on Windows.
2. The main window opens directly to the notes interface.
3. A user can create a note.
4. A user can edit note title and content.
5. Edited content is saved automatically.
6. A user can delete a note.
7. Deleted notes are hidden from the list.
8. Notes persist after closing and reopening the app.
9. The app handles the empty-note state gracefully.
10. The code is organized according to the agreed MVVM-style structure.
11. The MVP work is committed through feature branches and integrated into `develop`.
