# QingJian Tray and Minimize Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add native Windows tray residency, configurable minimize/close/startup behavior, and synchronized Settings controls.

**Architecture:** Persist `WindowBehaviorPreferences` in `AppSettings`, keep runtime decisions in a focused coordinator, and wrap `System.Windows.Forms.NotifyIcon` behind an event-based service. `MainWindow` remains responsible for editor-save-aware hiding and closing, while `App` composes tray commands and explicit shutdown.

**Tech Stack:** .NET 8, WPF, Windows Forms `NotifyIcon`, System.Drawing, xUnit.

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Do not add a third-party tray package.
- Use TDD for every production behavior change.
- Existing global hotkey, quick-note, desktop-todo, note autosave, and editor behavior must continue while the main window is hidden.

---

### Task 1: Persisted Window Behavior

**Files:**
- Create: `src/QingJian.App/Settings/WindowBehaviorPreferences.cs`
- Modify: `src/QingJian.App/Services/AppSettingsService.cs`
- Modify: `src/QingJian.App/Settings/SettingsSaveRequest.cs`
- Modify: `src/QingJian.App/Settings/SettingsWindowDraft.cs`
- Modify: `tests/QingJian.App.Tests/Services/AppSettingsServiceTests.cs`

**Interfaces:**
- Produces: `WindowBehaviorPreferences(bool MinimizeToTray, bool CloseToTray, bool StartMinimized)` and `WindowBehaviorPreferences.Default`.

- [ ] Add failing tests that old JSON loads `WindowBehaviorPreferences.Default` and a custom value round-trips.
- [ ] Run `dotnet test -c Release --no-restore --filter FullyQualifiedName~AppSettingsServiceTests` and verify missing members fail compilation.
- [ ] Add the record, JSON constructor parameter, convenience-constructor compatibility, normalization, request, and draft fields.
- [ ] Run the focused tests and require all to pass.
- [ ] Commit as `feat: persist tray window preferences`.

### Task 2: Testable Lifecycle Decisions

**Files:**
- Create: `src/QingJian.App/Tray/WindowBehaviorCoordinator.cs`
- Create: `tests/QingJian.App.Tests/Tray/WindowBehaviorCoordinatorTests.cs`

**Interfaces:**
- Consumes: `WindowBehaviorPreferences`.
- Produces: `Apply(WindowBehaviorPreferences)`, `ShouldHideOnMinimize(bool trayAvailable)`, `ShouldHideOnClose(bool explicitExit, bool trayAvailable)`, and `ShouldStartHidden(IReadOnlyList<string> args, bool trayAvailable)`.

- [ ] Add failing tests for default minimize-to-tray, default close-to-exit, explicit-exit bypass, no-tray fallback, ordinary launch, and `--startup` hidden launch.
- [ ] Run the focused tests and verify the type is missing.
- [ ] Implement a coordinator containing only current preferences and pure decisions.
- [ ] Run the focused tests and require all to pass.
- [ ] Commit as `feat: add tray lifecycle decisions`.

### Task 3: Native Tray Service

**Files:**
- Create: `src/QingJian.App/Tray/ITrayIconService.cs`
- Create: `src/QingJian.App/Tray/WindowsTrayIconService.cs`
- Create: `tests/QingJian.App.Tests/Tray/WindowsTrayIconServiceTests.cs`
- Modify: `src/QingJian.App/QingJian.App.csproj`

**Interfaces:**
- Produces: `OpenMainWindowRequested`, `QuickNoteRequested`, `ToggleTodoRequested`, `ExitRequested`, `IsAvailable`, `SetTodoVisible(bool)`, `Show()`, and `Dispose()`.

- [ ] Add failing source-contract tests requiring `NotifyIcon`, `ContextMenuStrip`, exact Chinese labels, double-click wiring, executable-icon fallback, dynamic todo label, and disposal.
- [ ] Run the focused tests and verify missing source files fail.
- [ ] Enable Windows Forms and implement the event-based tray service with no application-domain dependencies.
- [ ] Run tray tests and build the app to verify WPF/Windows Forms namespace compatibility.
- [ ] Commit as `feat: add native system tray service`.

### Task 4: Main Window Hide, Restore, and Exit

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`
- Create: `tests/QingJian.App.Tests/Tray/MainWindowTrayLifecycleTests.cs`

**Interfaces:**
- Consumes: `WindowBehaviorCoordinator`.
- Produces: `ApplyWindowBehaviorPreferences`, `ShowFromTray`, `RequestApplicationExit`, and `TrayAvailability` behavior.

- [ ] Add failing tests requiring save-before-hide, minimize state handling, close-to-tray cancellation, explicit-exit bypass, last-window-state restoration, taskbar visibility restoration, and save-failure visibility.
- [ ] Run the focused tests and verify required lifecycle members are absent.
- [ ] Inject the behavior coordinator, subscribe to `StateChanged`, and refactor the existing close-save path into one guarded asynchronous transition.
- [ ] Run focused main-window and editor tests and require all to pass.
- [ ] Commit as `feat: support save-aware minimize to tray`.

### Task 5: Settings UI and Transactional Runtime Apply

**Files:**
- Modify: `src/QingJian.App/Settings/SettingsWindow.xaml`
- Modify: `src/QingJian.App/Settings/SettingsWindow.xaml.cs`
- Modify: `src/QingJian.App/Settings/SettingsCoordinator.cs`
- Modify: `tests/QingJian.App.Tests/Settings/SettingsWindowXamlTests.cs`
- Modify: `tests/QingJian.App.Tests/Settings/SettingsWindowCodeBehindTests.cs`
- Modify: `tests/QingJian.App.Tests/Settings/SettingsCoordinatorTests.cs`

**Interfaces:**
- Consumes: `Func<WindowBehaviorPreferences, Task> applyWindowBehavior`.
- Produces: three `常规` checkboxes and transactional persistence/runtime application.

- [ ] Add failing XAML tests for exact checkbox labels and names `MinimizeToTrayCheckBox`, `CloseToTrayCheckBox`, and `StartMinimizedCheckBox`.
- [ ] Add failing code-behind tests for state loading, request capture, and startup-dependent enabled state.
- [ ] Add failing coordinator tests proving successful apply and rollback after a later runtime failure.
- [ ] Run focused settings tests and verify failures.
- [ ] Implement controls, draft flow, request values, settings update, runtime apply, and rollback.
- [ ] Run focused settings tests and commit as `feat: add tray behavior settings`.

### Task 6: Startup, Tray Commands, and Explicit Shutdown

**Files:**
- Modify: `src/QingJian.App/App.xaml`
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `src/QingJian.App/Settings/WindowsStartupRegistrationService.cs`
- Modify: `tests/QingJian.App.Tests/AppStartupWiringTests.cs`
- Modify: `tests/QingJian.App.Tests/Settings/WindowsStartupRegistrationServiceTests.cs`

**Interfaces:**
- Consumes: tray service, behavior coordinator, main-window lifecycle methods, quick-note coordinator, and todo coordinator.
- Produces: explicit shutdown, `--startup` handling, and complete tray command wiring.

- [ ] Add failing tests requiring `ShutdownMode="OnExplicitShutdown"`, startup command suffix `--startup`, tray event subscriptions, dynamic todo-label synchronization, hidden startup decision, and disposal in `OnExit`.
- [ ] Run focused wiring tests and verify failures.
- [ ] Compose the behavior and tray services, route events to existing coordinators, show or hide the main window after initialization, and explicitly shut down on exit request.
- [ ] Run startup, hotkey, todo-widget, and main-window tests.
- [ ] Commit as `feat: integrate tray application lifecycle`.

### Task 7: Full Verification

**Files:**
- Modify only files required by a reproducible regression.

- [ ] Run `dotnet test -c Release --no-restore` and require zero failures and zero skipped tests.
- [ ] Run `dotnet build -c Release --no-restore` and require zero warnings and zero errors.
- [ ] Run `git diff --check`, inspect branch history/status, and confirm `develop` retains only its pre-existing `outputs/` entry.
- [ ] Keep `feature/ui-polish` and `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish`; do not merge or remove them.
