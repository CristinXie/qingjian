# QingJian

QingJian is a native Windows sticky-notes app built with WPF, .NET 8, and SQLite.

## MVP

- Open directly to a notes interface.
- Create, edit, and soft-delete notes.
- Persist notes locally with SQLite.
- Save note edits automatically.

## Markdown Editing

- Note bodies are stored as Markdown text.
- The editor opens in WYSIWYG mode by default.
- Markdown source mode remains available inside the editor.
- Network image Markdown links are supported.
- Local image attachments are not part of the first Markdown editor version.

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
