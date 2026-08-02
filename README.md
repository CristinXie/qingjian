# QingJian

QingJian is a native Windows notes and desktop-todo app built with WPF, .NET 8, SQLite, EF Core, WebView2, and Toast UI Editor.

## Notes Workspace

- Create, edit, autosave, favorite, organize, search, and soft-delete notes.
- Switch between Markdown and WYSIWYG editing with undo/redo and plain-text statistics.
- Group navigation by modification date or sort by favorites.
- Organize each note in one single-level folder.
- Apply move, favorite, and delete actions in batch mode.
- Restore full note metadata from the 30-day recycle bin or delete notes permanently.

## Quick Note Shortcut

While QingJian is running, press `Ctrl + Alt + N` to open a quick-note card near the mouse pointer.

- Each shortcut press opens a new independent quick note.
- The global shortcut is configurable and can be disabled in Settings.
- Quick notes are borderless cards with no normal title bar buttons.
- The top strip is draggable and has one centered grip line as a visual hint.
- The lower-left footer shows the current line and character count.
- Leaving the title empty uses the first body line as the saved note title.
- `Ctrl + Enter` saves.
- `Esc` cancels and asks before discarding non-empty content.
- Tray settings determine whether minimizing or closing hides the main window while keeping shortcuts active.

## Desktop Todo Widget

QingJian shows a semi-transparent desktop todo widget by default on first launch after the feature is installed.

- The widget stores todos independently from notes.
- The main window can show or hide the widget.
- Subsequent app launches restore the widget's previous visible or hidden state.
- The widget supports 8-day, today-list, and monthly-calendar presets.
- A compact icon toolbar cycles modes, toggles lock state, and hides the widget while unlocked.
- The 8-day preset starts from yesterday and shows 8 consecutive days.
- The monthly-calendar preset supports previous month, next month, and return to current month.
- Todos can be added, edited, completed, deleted, and assigned no time, a single time, a same-day range, or an overnight range shorter than 24 hours.
- Completed todos remain visible and move to the bottom of their day.
- Widget visibility, mode, position, opacity, lock state, and calendar month are persisted.

## Settings And Tray

- Configure Windows startup, minimize/close-to-tray behavior, and startup-hidden behavior.
- Configure the default editor mode and global quick-note shortcut.
- Configure desktop todo visibility, mode, opacity, lock state, and reset behavior.
- Inspect local storage usage and runtime versions.
- Use the tray menu to open QingJian, create a quick note, show/hide desktop todo, or exit.

## Markdown Editing

- Note bodies are stored as Markdown text.
- The editor opens in WYSIWYG mode by default.
- Markdown source mode remains available inside the editor.
- Network image Markdown links are supported.
- Local image attachments are supported through toolbar upload, drag/drop, and paste.

## Project Status

For the current implementation state, branch workflow, known decisions, and new-conversation handoff instructions, see:

```text
docs/project-status.md
```

## Runtime Requirements

- Windows with the Microsoft Edge WebView2 Runtime installed.
- .NET 8 SDK for development.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

## Data Location

QingJian stores its database in:

```text
%LOCALAPPDATA%\QingJian\qingjian.db
```

Settings and attachments are stored under the same `%LOCALAPPDATA%\QingJian` directory.

## Branch Flow

Feature work starts from `develop`, uses `feature/*` branches, and merges back to `develop` after tests pass.

The application, tray, and WPF windows use the bundled `src\QingJian.App\Assets\qingjian.ico` icon.
