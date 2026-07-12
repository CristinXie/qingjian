# QingJian Project Status

Last updated: 2026-07-12
Stable branch: `develop`
Latest feature merge at update time: `b77f5f1 merge: hotkey quick note feature`

## Purpose

This document is the handoff file for new Codex conversations. Before starting a new feature, read this file, then inspect the current git status and recent commits. Each conversation should own one independent feature branch and merge back to `develop` only after verification passes.

## Project Summary

QingJian is a native Windows sticky-notes app built with WPF, .NET 8, SQLite, EF Core, and WebView2.

The app currently supports a local notes workflow:

- Launches into a full WPF notes UI.
- Creates notes.
- Edits note titles and note bodies.
- Soft-deletes notes.
- Persists data locally in SQLite.
- Saves note changes automatically.
- Uses a Toast UI based Markdown editor hosted in WebView2.

## Current Architecture

- `src/QingJian.App/App.xaml.cs`
  - Creates the app data folder under `%LOCALAPPDATA%\QingJian`.
  - Configures SQLite database access.
  - Wires repositories, services, view models, and `MainWindow`.

- `src/QingJian.App/Data/`
  - EF Core `AppDbContext`.
  - `NoteRepository` for note persistence.

- `src/QingJian.App/Services/`
  - `NoteService`: note creation, update, soft delete behavior.
  - `AppSettingsService`: JSON settings stored in `%LOCALAPPDATA%\QingJian\settings.json`.
  - `AttachmentService`: local image attachment storage under `%LOCALAPPDATA%\QingJian\attachments`.

- `src/QingJian.App/ViewModels/`
  - `MainViewModel` drives the note list, selected note, and commands.

- `src/QingJian.App/Views/`
  - `MainWindow.xaml` and `MainWindow.xaml.cs` host the primary UI and bridge WPF with the WebView2 Markdown editor.

- `src/QingJian.App/Editor/`
  - `EditorMessage`: parses messages posted from the WebView editor.
  - `MarkdownEditorState`: guards editor sync and stale note updates.

- `src/QingJian.App/EditorAssets/`
  - `index.html`, `editor-host.js`, `editor-host.css`.
  - Vendored Toast UI editor assets for offline runtime use.

## Completed Features

### MVP Notes

- WPF main notes interface.
- Create note.
- Select note.
- Edit note title.
- Edit note body.
- Soft delete selected note.
- SQLite persistence.
- Restore notes after restart.

### Title Placeholder

- New notes use the default title `未命名文件`.
- The default title appears grey when not editing.
- When preparing to edit the default title, the placeholder text clears.
- If the user leaves the title empty, it returns to `未命名文件`.

### Markdown Editor

- Note content is stored as Markdown.
- Toast UI editor is hosted inside WebView2.
- WYSIWYG and Markdown source modes are supported.
- Last used editor mode is preserved globally across app restarts.
- Chinese font compatibility is improved through editor CSS.
- Markdown link handling is stabilized:
  - Normal link clicks do not navigate away inside the editor.
  - Ctrl/meta click requests external browser opening.
  - WebView navigation away from the editor page is blocked.
- Network image Markdown URLs are supported.
- Local image attachments are supported:
  - Toolbar image upload works.
  - Dragging local image files into the editor works through the WebView path.
  - Copy/paste image files works.
  - Copy/paste clipboard screenshots/images works through native paste fallback.
  - Inserted images are copied into `%LOCALAPPDATA%\QingJian\attachments`, so notes do not depend on the original file path.

### Hotkey Quick Notes

- `Ctrl + Alt + N` opens a new independent quick-note window while the app is running.
- Every shortcut press opens a separate quick-note window; there is no single-window cap.
- Quick-note windows are borderless, clean white cards that stay out of the taskbar.
- Quick-note windows have no normal title bar, minimize button, or close button.
- The top strip remains the full draggable hit area.
- Three short centered grip lines near the top visually indicate the draggable area without shrinking the hit area.
- The lower-left footer shows the current body line and character count instead of shortcut hints.
- Quick-note titles show a grey `标题` placeholder until the user focuses the title field.
- Blank quick-note titles are generated from the first body line.
- `Ctrl + Enter` saves the quick note.
- `Esc` cancels; non-empty drafts ask before discarding and no longer re-enter close logic.
- Quick notes save through the existing local note storage.
- Visible main windows receive saved quick notes immediately; minimized windows do not steal focus.

## Known Decisions

- Editor mode persistence is global, not per note.
- Local attachments are copied into app data, not referenced by original file path.
- No unused attachment cleanup yet.
- No image compression yet.
- No cloud sync yet.
- No dedicated attachment manager yet.
- Markdown task checkbox click behavior in Markdown mode is intentionally postponed.
- Font-size controls are postponed until after the current Markdown feature review.
- Quick-note shortcut customization is postponed until a future shortcut settings page.
- Tray residency is postponed; closing the main window exits the app, so the global shortcut only works while QingJian is running.
- Quick-note color selection is postponed; quick notes currently use a white background and neutral border.

## Validation Commands

Run these before claiming a feature branch is complete:

```powershell
dotnet test
dotnet build
```

For WPF development, avoid running `dotnet test` and `dotnet build` in parallel because generated output files can be locked.

Run the app with:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

If the app is already running, close it before building or testing to avoid `QingJian.App.exe` file locks.

## Branch Workflow

- `develop` is the stable integration branch.
- New work should start from `develop`.
- Use one feature branch per conversation/task.
- Suggested branch names:
  - `feature/font-size`
  - `feature/hotkey-settings`
  - `feature/desktop-schedule`
- Do not commit directly to `develop` during feature work.
- Merge back to `develop` only after tests/build pass and the user accepts the behavior.

Current worktree layout at the time this document was updated:

- Main workspace: `C:\Users\Cristin\Desktop\VibeCoding\qingjian`
- Current branch in main workspace: `develop`
- No active feature worktree is expected after the hotkey quick-note merge.

Always confirm the current layout with:

```powershell
git worktree list
git status --short --branch
```

## Suggested Prompt For New Conversations

Use this at the start of a new task:

```text
这是 qingjian 项目。请先阅读 docs/project-status.md、README.md、docs/superpowers/specs 和 docs/superpowers/plans，然后运行 git status、git branch、git log -5、git worktree list。以 develop 为基础，为本次功能创建独立分支。不要直接改 develop，完成并通过测试后再合并回 develop。
```

## Suggested Next Features

- Font-size controls for selected text and future typing.
- Shortcut settings page, including changing the quick-note hotkey.
- Tray residency, if shortcuts should keep working after the main window is closed.
- Optional quick-note color selection.
- Desktop transparent todo/schedule overlay.
- Attachment cleanup or attachment manager.
- Markdown task checkbox click support, if needed later.

## Update Rule

Whenever a feature is merged into `develop`, update this file in the same merge or immediately after it:

- Add completed features.
- Add changed architecture decisions.
- Add known issues or postponed work.
- Update validation notes if commands change.
- Update latest stable branch/commit information if useful.
