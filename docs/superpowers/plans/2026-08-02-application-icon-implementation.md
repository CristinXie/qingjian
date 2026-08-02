# QingJian Application Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the supplied QingJian ICO the single icon for the executable, tray, taskbar, and all WPF windows.

**Architecture:** Copy the original ICO without modification into the app's `Assets` directory, configure it as both the executable icon and a WPF resource, and explicitly reference one pack URI from every `Window` root. Existing tray extraction then inherits the executable icon automatically.

**Tech Stack:** .NET 8, WPF, MSBuild, xUnit, Windows ICO.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Preserve the source ICO bytes and SHA-256 `1348A3FD637AFBD61470B38B72A873127BBD93AB8EE608FDB6BED4CBCEC6D51D`.
- Use TDD before changing production configuration or files.

---

### Task 1: Icon Resource Contract

**Files:**
- Create: `tests/QingJian.App.Tests/ApplicationIconResourceTests.cs`
- Create: `src/QingJian.App/Assets/qingjian.ico`
- Modify: `src/QingJian.App/QingJian.App.csproj`
- Modify: all seven production `Window` XAML files.

**Interfaces:**
- Produces: shared pack URI `/QingJian.App;component/Assets/qingjian.ico` and executable icon path `Assets\qingjian.ico`.

- [ ] Add a failing test that parses `QingJian.App.csproj`, requires `ApplicationIcon` and `Resource Include`, verifies the icon SHA-256, enumerates all XAML `Window` roots, and requires the shared `Icon` URI.
- [ ] Run `dotnet test -c Release --no-restore --filter FullyQualifiedName~ApplicationIconResourceTests` and verify failure because the project resource does not exist.
- [ ] Copy `C:\Users\Cristin\Desktop\qingjian.ico` byte-for-byte to `src\QingJian.App\Assets\qingjian.ico`.
- [ ] Add `<ApplicationIcon>Assets\qingjian.ico</ApplicationIcon>` and `<Resource Include="Assets\qingjian.ico" />` to the project file.
- [ ] Add `Icon="/QingJian.App;component/Assets/qingjian.ico"` to all seven `Window` roots.
- [ ] Run the focused test and tray tests and require all to pass.
- [ ] Commit as `feat: apply QingJian application icon`.

### Task 2: Verification

**Files:**
- Modify only files required by a reproducible regression.

- [ ] Run `dotnet test -c Release --no-restore` and require zero failures and zero skipped tests.
- [ ] Run `dotnet build -c Release --no-restore` and require zero warnings and zero errors.
- [ ] Verify the source and project icon SHA-256 values remain identical.
- [ ] Run `git diff --check` and confirm `feature/ui-polish` is clean while `develop` retains only its pre-existing `outputs/` entry.
