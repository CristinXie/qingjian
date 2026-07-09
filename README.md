# QingJian

QingJian is a native Windows sticky-notes app built with WPF, .NET 8, and SQLite.

## MVP

- Open directly to a notes interface.
- Create, edit, and soft-delete notes.
- Persist notes locally with SQLite.
- Save note edits automatically.

## Development

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src\QingJian.App\QingJian.App.csproj
```
