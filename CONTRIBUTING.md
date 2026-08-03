# Contributing to QingJian

Thanks for helping improve QingJian.

## Development Setup

- Windows with the Microsoft Edge WebView2 Runtime.
- .NET 8 SDK.
- Git and PowerShell.

From the repository root:

```powershell
dotnet restore
dotnet test -c Release --no-restore
dotnet build -c Release --no-restore
```

Run the application with:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

WPF test and build commands should run sequentially because generated files
can be locked by parallel processes.

## Branches And Pull Requests

- Start feature work from `develop` in a `feature/*` branch.
- Keep commits focused and explain user-visible behavior in the commit message.
- Add or update tests with behavior changes.
- Target pull requests at `develop`; `main` is the public release branch.
- Describe manual Windows verification for tray, WebView2, installer, or startup changes.

## Code And UI

- Preserve local data compatibility and never replace an existing database destructively.
- Keep user data local unless a feature explicitly documents network behavior.
- Follow the existing WPF, MVVM, and service boundaries.
- Use the existing icon-button and tooltip conventions for compact actions.
- Keep user-facing text consistent with the current Chinese UI.
