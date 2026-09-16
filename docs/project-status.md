# QingJian Project Status

Last updated: 2026-08-03
Stable public branch: `main`
Latest feature merge at update time: `49d59de merge: integrate single-instance setup`

## Purpose

This is the primary handoff document for new Codex conversations. Read it before changing code, then inspect `git status`, recent commits, and `git worktree list`. New feature work must start from `develop` in an independent branch/worktree and return to `develop` only after acceptance and verification.

## Project Summary

QingJian is a native Windows notes and desktop-todo application built with WPF, .NET 8, SQLite, EF Core, WebView2, Toast UI Editor, and Markdig. It stores all user data locally under `%LOCALAPPDATA%\QingJian`.

The current application includes:

- A searchable, grouped notes workspace with favorite and folder organization.
- Markdown and WYSIWYG editing with local image attachments.
- Global-hotkey quick notes.
- A configurable desktop todo widget with overnight todo support.
- Folder management, batch note management, and a 30-day recycle bin.
- Settings for startup, tray behavior, editing, hotkeys, todo appearance, and runtime/data information.
- A system tray menu and save-aware minimize/close behavior.

## Current Architecture

- `src/QingJian.App/App.xaml.cs`
  - Creates application services and windows.
  - Initializes SQLite, settings, the global hotkey, desktop todo coordinator, and tray lifecycle.
  - Uses explicit application shutdown so tray residency can keep the process alive.

- `src/QingJian.App/Lifecycle/`
  - `SingleInstanceCoordinator` enforces one running QingJian instance and activates the existing window when a second launch is attempted.

- `installer/` and `scripts/`
  - `QingJian.iss` defines the Windows installer payload and shortcuts.
  - `build-installer.ps1` bootstraps Inno Setup through NuGet and builds a distributable installer.

- `src/QingJian.App/Data/`
  - `AppDbContext` owns the local SQLite schema and compatibility initialization.
  - `NoteRepository`, `FolderRepository`, and `TodoRepository` provide persistence and transactional workflows.
  - Note timestamps are normalized consistently so reload, recycle, restore, and permanent deletion do not shift unrelated modification times.

- `src/QingJian.App/Services/`
  - `NoteService` handles create, update, favorite, move, soft delete, 30-day recycle queries, restore, permanent deletion, and batch operations.
  - `FolderService` enforces single-level folders, unique normalized names, and the protected `未分类` system folder.
  - `TodoService` handles todo persistence and ordering, including valid overnight ranges shorter than 24 hours.
  - `AppSettingsService` stores JSON preferences in `%LOCALAPPDATA%\QingJian\settings.json`.
  - `AttachmentService` copies local images into `%LOCALAPPDATA%\QingJian\attachments`.

- `src/QingJian.App/ViewModels/`
  - `MainViewModel` drives selection, navigation groups, search, sorting, favorites, folders, and batch actions.
  - `FolderManagementViewModel`, `RecycleBinViewModel`, and `BatchSelectionState` own their focused workflows.
  - Navigation helpers implement plain-text Markdown search, date grouping, and time/favorite ordering.

- `src/QingJian.App/Views/`
  - `MainWindow` hosts the primary workspace and the WPF/WebView2 editor bridge.
  - `FolderManagementWindow` and `FolderNameDialog` manage single-level folders.
  - `RecycleBinWindow` presents deleted-note cards plus title/content preview and restore/permanent-delete commands.

- `src/QingJian.App/Settings/`
  - `SettingsWindow` exposes General, Editing, Quick Note, Desktop Todo, Data, and About tabs.
  - `SettingsCoordinator` validates and applies preferences without partially saving invalid changes.
  - Startup registration is managed through the current-user Windows Run registry key.

- `src/QingJian.App/Tray/`
  - `WindowsTrayIconService` owns the native tray icon and Chinese menu.
  - `WindowBehaviorCoordinator` decides minimize, close, startup, restore, and explicit-exit behavior.

- `src/QingJian.App/TodoWidgets/`
  - `TodoWidgetCoordinator` owns startup restoration, visibility, and window lifetime.
  - `TodoWidgetViewModel` exposes 8-day, today-list, and monthly-calendar views over shared data.
  - `TodoWidgetWindow` is a borderless, translucent, pale-green surface with scrollable add/edit popovers.

- `src/QingJian.App/EditorAssets/`
  - `editor-host.js` and `editor-host.css` integrate vendored Toast UI Editor assets.
  - Editor synchronization preserves the caret across autosave and prevents duplicate pasted images.

## Completed Features

### Main Notes Workspace

- Creates, selects, edits, autosaves, and soft-deletes notes.
- New notes become selected in both the editor and navigation immediately.
- Delete requires confirmation; the editor toolbar uses Chinese-tooltipped icon buttons.
- Title `Tab` navigation moves directly into the body editor.
- Toolbar actions switch Markdown/WYSIWYG mode, undo, redo, and delete.
- `Ctrl+Y` restores an undone editor operation.
- Body statistics count plain-text lines and characters, excluding Markdown syntax and editor-generated line-break markup.
- Creation date and body statistics share the editor footer without a divider.
- Navigation search ranks title matches ahead of body matches.
- Time sorting is descending by last modification time.
- Favorite sorting separates favorite/non-favorite notes, orders favorites by favorite time, and other notes by modification time.
- Navigation cards show title, favorite star, modification time, and folder with balanced spacing.
- Time navigation groups are `今天`, `过去30天`, months in the current year, and specific older years.
- Notes can be favorited from navigation; the sort icon represents the active mode.
- All relevant text prompts are Chinese except the product term `Markdown`.

### Folder And Batch Management

- Every note belongs to exactly one single-level folder.
- `未分类` is the protected default/system folder.
- Folders can be created, renamed, selected as filters, and deleted according to folder rules.
- Notes can be moved between valid folders.
- Batch mode supports checkbox selection, select-all/clear-selection behavior, moving, favorite changes, and confirmed deletion.
- The batch clear-selection command uses a borderless trash icon consistent with the rest of the UI.

### Recycle Bin

- The navigation footer includes a recycle-bin icon button.
- The recycle bin lists notes deleted within the last 30 days using navigation-style cards without favorite stars.
- Selecting a recycled note shows its title and content preview.
- Restore recovers note content, folder, favorite state/time, creation time, and modification time.
- Permanent deletion executes directly from the recycle-bin card.
- Recycle actions preserve timestamps of both the affected note and unrelated active notes.

### Markdown And Attachments

- Note content is stored as Markdown and edited through Toast UI Editor in WebView2.
- WYSIWYG and Markdown source modes are supported and the selected default mode is configurable.
- External links cannot navigate the embedded editor; Ctrl/meta-click opens them externally.
- Network images and local attachments are supported.
- Toolbar upload, drag/drop, copied files, and clipboard images all copy into app storage.
- Clipboard insertion is deduplicated so one paste does not produce two images.
- Caret position remains stable while body autosave and statistics updates run.

### Quick Notes

- A configurable global shortcut opens independent quick-note windows; the default is `Ctrl+Alt+N`.
- The shortcut can be disabled or changed in Settings.
- Quick notes have a borderless design, single-line drag indicator, body statistics, and lower footer actions.
- Blank titles are generated from the first body line.
- `Ctrl+Enter` saves; `Esc` cancels and protects non-empty drafts.

### Desktop Todo Widget

- The widget uses a semi-transparent pale-green background and icon-only chrome.
- The main window and tray menu use explicit `显示桌面待办` / `隐藏桌面待办` actions according to current state.
- 8-day, today-list, and monthly-calendar modes share persisted todo data.
- Todo add/edit popovers are pale green and vertically scroll when content grows.
- Todos support no time, one start time, a same-day range, or an overnight range.
- When start time is later than end time, the end is interpreted as the next day if total duration is less than 24 hours; otherwise the draft is invalid.
- Overnight todos are counted against their start date and display `次日`, with the end time wrapped where necessary.
- Completion, ordering, calendar navigation, locking, position, opacity, mode, and visibility are persisted.
- The widget remains an ordinary borderless WPF window at the bottom of normal window Z order rather than attaching to WorkerW/Progman.

### Settings, Tray, And Application Lifecycle

- Settings tabs: General, Editing, Quick Note, Desktop Todo, Data, and About.
- General options include Windows startup, minimize to tray, close to tray, and start hidden when auto-started.
- Defaults: minimize to tray on, close to tray off, startup hidden off.
- Desktop todo settings include visibility, mode, opacity, lock state, and resetting position/appearance.
- Data settings show the data folder and database/attachment/total size.
- About reports app, .NET, and WebView2 runtime versions.
- Tray double-click restores the main window.
- Tray menu actions open the main window, create a quick note, show/hide desktop todo, and exit.
- Minimize/close-to-tray saves real pending changes before hiding.
- Explicit tray exit shuts down cleanly; tray setup failure falls back to ordinary window behavior.
- `src/QingJian.App/Assets/qingjian.ico` is the EXE, tray, and all-window application icon.
- A second launch activates the existing instance instead of opening another application process.
- WebView2 user data is stored under the QingJian local application data directory.
- The repository includes an Inno Setup definition and a PowerShell installer build script.

## Data And Compatibility

- Database: `%LOCALAPPDATA%\QingJian\qingjian.db`
- Settings: `%LOCALAPPDATA%\QingJian\settings.json`
- Attachments: `%LOCALAPPDATA%\QingJian\attachments`
- Existing databases are upgraded in place by compatibility initialization; do not replace this with destructive recreation.
- Deleted notes retain metadata required for lossless restoration.
- Timestamps are persisted as UTC and converted only for display.

## Known Decisions And Deferred Work

- A note belongs to one folder only; nested folders are not supported.
- Backup and restore are intentionally not included yet.
- No cloud sync, attachment cleanup/compression, or dedicated attachment manager yet.
- Markdown task-checkbox interaction in source mode remains deferred.
- Font-size controls remain deferred.
- Todo reminders/notifications and repeating todos remain deferred.
- Overnight todos may cross midnight but never span 24 hours or more.
- The desktop todo uses a fixed base size; size presets remain deferred.
- The lower-left Settings, Folder, Batch, and Recycle Bin buttons are implemented; no separate main-window todo management page exists.

## Validation Baseline

Run sequentially from the repository root:

```powershell
dotnet restore
dotnet test -c Release --no-restore
dotnet build -c Release --no-restore
git diff --check
```

Do not run WPF test/build commands in parallel because generated files can lock each other. If the application is already running, avoid forcibly closing it when the user may have unsaved editing state.

Latest verified merged baseline on 2026-08-02:

- 429 tests passed, 0 failed, 0 skipped.
- Release build completed with 0 warnings and 0 errors.

Latest merged baseline on 2026-08-03:

- 440 tests passed, 0 failed, 0 skipped after merging single-instance and installer support.
- Release build completed with 0 warnings and 0 errors after the merge.

Run the application with:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

## Branch And Worktree Workflow

- `develop` is the stable integration branch; `main` is the public release branch.
- Do not develop features directly on `develop`.
- Create one independent `feature/*` branch and worktree per task.
- Merge into `develop` only after tests/build pass and the user accepts the behavior.
- When the user says `完成分支收尾工作`, merge the current feature branch into `develop`, then update this handoff document on `develop`.
- Preserve unrelated or user-owned working-tree changes.

Current layout at this update:

- Main workspace: `C:\Users\Cristin\Desktop\VibeCoding\qingjian` on `main`.
- UI polish was merged into `develop` as `6f00bdb`.
- Single-instance and installer support was merged into `develop` as `49d59de`.
- Existing external worktree: `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on the merged `feature/ui-polish` branch. Confirm ownership before removing it.
- Existing external worktree: `C:\Users\Cristin\Desktop\VibeCoding\qingjian-single-instance-setup` on the merged `feature/single-instance-setup` branch. Confirm ownership before removing it.
- The application icon source images under `assets/branding/` are tracked in `f99f792`.

Always check current state rather than relying only on this snapshot:

```powershell
git status --short --branch
git log -5 --oneline --decorate
git worktree list
```

## Suggested Prompt For New Conversations

```text
这是 qingjian 项目。请先阅读 docs/project-status.md、README.md，以及与本次任务有关的 docs/superpowers/specs 和 docs/superpowers/plans；然后检查 git status、git log -5 和 git worktree list。以 develop 为基础创建独立 feature 分支和 worktree，不要直接在 develop 开发。完成并通过测试后，等待我确认再合并。
```

## Suggested Next Features

- Backup and restore.
- Desktop todo reminders/notifications and repeating todos.
- Font-size controls.
- Attachment cleanup, compression, or management.
- Markdown task-checkbox interaction.
- Desktop todo size presets or a full todo-management window.

## Update Rule

Whenever a feature is merged into `develop`, update this file immediately afterward with completed behavior, architectural changes, deferred work, verification results, and current worktree facts.
