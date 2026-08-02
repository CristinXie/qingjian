# QingJian Single Instance And Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce one QingJian process per user session, relocate WebView2 data to local application data, and build a desktop-delivered Windows x64 Setup executable.

**Architecture:** A focused lifecycle coordinator owns a per-session named mutex and activation event, while `App` gates startup before creating any application services. `MainWindow` receives an explicit WebView2 user-data folder. A repository-owned PowerShell/Inno Setup pipeline publishes the app and compiles a per-user installer.

**Tech Stack:** .NET 8, WPF, named Windows synchronization objects, WebView2, xUnit, PowerShell, Inno Setup 6.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-single-instance-setup` on `feature/single-instance-setup`.
- Do not modify or merge `develop` during implementation.
- Keep the existing `%LOCALAPPDATA%\QingJian` database, settings, and attachment locations unchanged.
- Use `%LOCALAPPDATA%\QingJian\WebView2` for WebView2 runtime data.
- Publish a Windows x64 self-contained single-file app.
- Install per user under `%LOCALAPPDATA%\Programs\QingJian`.
- Use icon-only borderless UI conventions; this change adds no new in-app buttons.
- Implement production behavior only after the corresponding test has failed for the expected reason.

---

### Task 1: Single-Instance Coordinator

**Files:**
- Create: `src/QingJian.App/Lifecycle/SingleInstanceCoordinator.cs`
- Create: `tests/QingJian.App.Tests/Lifecycle/SingleInstanceCoordinatorTests.cs`

**Interfaces:**
- Produces: `SingleInstanceCoordinator.Create(string applicationKey)`.
- Produces: `bool IsPrimary`, `bool NotifyPrimary()`, `void StartListening(Action activationRequested)`, and `Dispose()`.
- Uses: a named auto-reset event and named mutex prefixed with `Local\`.

- [ ] **Step 1: Write failing ownership and activation tests**

Create tests with a unique key per test. Verify the first coordinator is primary, the second is secondary, a secondary signal completes a primary listener `TaskCompletionSource`, and ownership is available again after both handles are disposed.

```csharp
var key = $"QingJian.Tests.{Guid.NewGuid():N}";
using var primary = SingleInstanceCoordinator.Create(key);
using var secondary = SingleInstanceCoordinator.Create(key);
Assert.True(primary.IsPrimary);
Assert.False(secondary.IsPrimary);

var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
primary.StartListening(() => activated.TrySetResult());
Assert.True(secondary.NotifyPrimary());
await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

- [ ] **Step 2: Run RED**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SingleInstanceCoordinatorTests
```

Expected: compilation fails because `QingJian.App.Lifecycle.SingleInstanceCoordinator` does not exist.

- [ ] **Step 3: Implement the coordinator**

Create both named synchronization objects in `Create`. The event must be created before mutex acquisition so an activation signal survives a startup race. The listener uses `WaitHandle.WaitAny` over the activation event and a private cancellation event. `Dispose` stops and joins the listener, releases the mutex only for the primary, and disposes all handles.

- [ ] **Step 4: Run GREEN and full lifecycle tests**

Run the focused command from Step 2 and confirm all coordinator tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\QingJian.App\Lifecycle tests\QingJian.App.Tests\Lifecycle
git commit -m "feat: coordinate single application instance"
```

---

### Task 2: Gate WPF Startup And Restore The Primary Window

**Files:**
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `tests/QingJian.App.Tests/AppStartupWiringTests.cs`

**Interfaces:**
- Consumes: `SingleInstanceCoordinator.Create`, `IsPrimary`, `NotifyPrimary`, `StartListening`, and `Dispose`.
- Consumes: existing `MainWindow.ShowFromTray()`.

- [ ] **Step 1: Add failing startup-order contract tests**

Assert source ordering, not only source presence:

```csharp
var gateIndex = source.IndexOf("SingleInstanceCoordinator.Create", StringComparison.Ordinal);
var databaseIndex = source.IndexOf("new AppDbContext", StringComparison.Ordinal);
Assert.True(gateIndex >= 0 && gateIndex < databaseIndex);
Assert.Contains("if (!_singleInstanceCoordinator.IsPrimary)", source);
Assert.Contains("_singleInstanceCoordinator.NotifyPrimary();", source);
Assert.Contains("Shutdown();", source);
Assert.Contains("Dispatcher.BeginInvoke(window.ShowFromTray)", source);
Assert.Contains("_singleInstanceCoordinator?.Dispose();", source);
```

- [ ] **Step 2: Run RED**

Run `dotnet test ... --filter FullyQualifiedName~AppStartupWiringTests` and confirm the missing single-instance wiring causes failure.

- [ ] **Step 3: Integrate the coordinator**

Add a field to `App`. Immediately after `base.OnStartup(e)`, create the coordinator with the stable key `QingJian.6DCE9EF8-0E5B-4705-86CB-4D397226C321`. For a secondary process, notify, shut down, and return before computing `appDataFolder`. After assigning `MainWindow`, start listening with a callback that dispatches `window.ShowFromTray`. Dispose the coordinator last in `OnExit`.

- [ ] **Step 4: Run GREEN and tray lifecycle regression tests**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AppStartupWiringTests|FullyQualifiedName~MainWindowTrayLifecycleTests"
```

- [ ] **Step 5: Commit**

```powershell
git add src\QingJian.App\App.xaml.cs tests\QingJian.App.Tests\AppStartupWiringTests.cs
git commit -m "feat: activate the existing QingJian instance"
```

---

### Task 3: Relocate WebView2 Runtime Data

**Files:**
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/AppStartupWiringTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`

**Interfaces:**
- `MainWindow` final constructor gains `string webView2UserDataFolder`.
- Existing shorter constructor computes the same local-app-data default for compatibility.

- [ ] **Step 1: Add failing WebView2 path tests**

Assert `App` constructs `Path.Combine(appDataFolder, "WebView2")` and passes it to `MainWindow`. Assert `MainWindow` calls:

```csharp
var environment = await CoreWebView2Environment.CreateAsync(
    userDataFolder: _webView2UserDataFolder);
await MarkdownWebView.EnsureCoreWebView2Async(environment);
```

Also assert the parameter is stored and the parameterless `EnsureCoreWebView2Async()` call no longer exists.

- [ ] **Step 2: Run RED**

Run focused `AppStartupWiringTests|MainWindowCodeBehindTests`; expected failure is missing explicit WebView2 environment wiring.

- [ ] **Step 3: Implement explicit environment creation**

Compute the folder once in `App`, pass it through the composition root, save it in `MainWindow`, and initialize WebView2 with `CoreWebView2Environment.CreateAsync`. Keep the existing fallback catch block unchanged.

- [ ] **Step 4: Run GREEN**

Run the focused command from Step 2 and confirm all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\QingJian.App\App.xaml.cs src\QingJian.App\Views\MainWindow.xaml.cs tests\QingJian.App.Tests\AppStartupWiringTests.cs tests\QingJian.App.Tests\Views\MainWindowCodeBehindTests.cs
git commit -m "fix: store WebView2 data under local app data"
```

---

### Task 4: Define A Repeatable Installer Pipeline

**Files:**
- Create: `installer/QingJian.iss`
- Create: `scripts/build-installer.ps1`
- Create: `tests/QingJian.App.Tests/Installer/InstallerDefinitionTests.cs`
- Modify: `.gitignore`
- Modify: `src/QingJian.App/QingJian.App.csproj`

**Interfaces:**
- Script parameters: `OutputDirectory`, optional `InnoCompilerPath`, optional `WebView2BootstrapperPath`.
- Inno preprocessor variables: `PublishDir`, `WebView2Bootstrapper`, `OutputDir`.
- Output: `<OutputDir>\QingJian-Setup.exe`.

- [ ] **Step 1: Add failing installer definition tests**

Parse the text assets and require:

- Project version `0.1.0`.
- Stable `AppId`.
- `PrivilegesRequired=lowest` and `{localappdata}\Programs\QingJian`.
- x64-compatible architecture.
- Start Menu and default-selected desktop shortcuts.
- Source `QingJian.App.exe` installed as `QingJian.exe`.
- Setup/uninstall icon resource.
- WebView2 registry detection using client ID `{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`.
- Conditional silent bootstrapper execution.
- Setup output base filename `QingJian-Setup`.
- PowerShell publish arguments `win-x64`, `--self-contained true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, and `IncludeAllContentForSelfExtract=true`.
- Inno invocation receives all three preprocessor paths and verifies the final file.

- [ ] **Step 2: Run RED**

Run `dotnet test ... --filter FullyQualifiedName~InstallerDefinitionTests`; expected failure is missing installer files and project version.

- [ ] **Step 3: Implement installer and build script**

Add `artifacts/` to `.gitignore`. Set `<Version>0.1.0</Version>` in the app project. Write an Inno per-user installer that renames the published host, creates shortcuts, registers uninstall metadata, checks WebView2 in HKLM/HKCU EdgeUpdate client keys, and runs the bootstrapper only when absent. Write a fail-fast PowerShell script that publishes to `artifacts/publish/win-x64`, downloads `https://go.microsoft.com/fwlink/p/?LinkId=2124703` into `artifacts/cache` when needed, invokes `ISCC.exe`, and returns the verified Setup path.

- [ ] **Step 4: Run GREEN**

Run the focused installer definition tests and `git diff --check`.

- [ ] **Step 5: Commit**

```powershell
git add .gitignore src\QingJian.App\QingJian.App.csproj installer scripts tests\QingJian.App.Tests\Installer
git commit -m "build: add QingJian setup pipeline"
```

---

### Task 5: Configure Tooling And Produce The Desktop Setup

**Files:**
- Generated, ignored: `artifacts/**`
- Generated delivery: `C:\Users\Cristin\Desktop\QingJian-Setup.exe`

**Interfaces:**
- Consumes: `scripts/build-installer.ps1`.
- Produces: a verified PE Setup executable on the desktop.

- [ ] **Step 1: Install Inno Setup Compiler when absent**

```powershell
winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements
```

Resolve `ISCC.exe` from `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` or `C:\Program Files\Inno Setup 6\ISCC.exe`.

- [ ] **Step 2: Run complete source verification sequentially**

```powershell
dotnet test -c Release --no-restore
dotnet build -c Release --no-restore
git diff --check
```

Expected: all tests pass and Release build reports zero warnings/errors.

- [ ] **Step 3: Build Setup**

```powershell
.\scripts\build-installer.ps1 -OutputDirectory "$env:USERPROFILE\Desktop"
```

Expected: the script reports `C:\Users\Cristin\Desktop\QingJian-Setup.exe`.

- [ ] **Step 4: Verify artifact and repository isolation**

Verify the Setup begins with `MZ`, has a nonzero length, has a SHA-256 hash, and is the only desktop file changed by delivery. Confirm `git status --short` is clean in the feature worktree and `develop` remains at `80453ec` with only its existing `outputs/` directory.

- [ ] **Step 5: Report delivery without merging**

Report the branch, commits, test/build counts, installer path/size/hash, and WebView2 runtime behavior. Do not merge into `develop` until the user explicitly says `完成分支收尾工作`.
