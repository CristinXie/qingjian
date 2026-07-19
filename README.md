# QingJian

QingJian is a native Windows sticky-notes app built with WPF, .NET 8, and SQLite.

## MVP

- Open directly to a notes interface.
- Create, edit, and soft-delete notes.
- Persist notes locally with SQLite.
- Save note edits automatically.

## Quick Note Shortcut

While QingJian is running, press `Ctrl + Alt + N` to open a quick-note card near the mouse pointer.

- Each shortcut press opens a new independent quick note.
- Quick notes are clean white, borderless-style cards with no normal title bar buttons.
- The top strip is draggable and has three short grip lines as a visual hint.
- The lower-left footer shows the current line and character count.
- Leaving the title empty uses the first body line as the saved note title.
- `Ctrl + Enter` saves.
- `Esc` cancels and asks before discarding non-empty content.
- Closing the main window exits the app, so the shortcut stops working after exit.

## Desktop Todo Widget

QingJian shows a semi-transparent desktop todo widget by default on first launch after the feature is installed.

- The widget stores todos independently from notes.
- The main window can show or hide the widget.
- Subsequent app launches restore the widget's previous visible or hidden state.
- The widget supports 8-day, today-list, and monthly-calendar presets.
- A compact icon toolbar cycles modes, toggles lock state, and hides the widget while unlocked.
- The 8-day preset starts from yesterday and shows 8 consecutive days.
- The monthly-calendar preset supports previous month, next month, and return to current month.
- Todos can be added, edited, completed, deleted, and assigned no time, a single time, or a time range from the widget.
- Completed todos remain visible and move to the bottom of their day.
- Widget visibility, mode, position, opacity, lock state, and calendar month are persisted.

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

The MVP stores notes in:

```text
%LOCALAPPDATA%\QingJian\qingjian.db
```

## Branch Flow

Feature work starts from `develop`, uses `feature/*` branches, and merges back to `develop` after tests pass.

## v0.1.0 Acceptance

- Launches as a WPF Windows app.
- Opens directly to the notes interface.
- Creates notes.
- Edits title and content.
- Saves edits automatically.
- Soft-deletes notes.
- Restores notes after restart.
