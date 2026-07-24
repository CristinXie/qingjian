# Settings Center Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a functional six-section QingJian settings window for startup registration, editor mode, quick-note hotkey, desktop todo preferences, storage information, and runtime information.

**Architecture:** Extend the backward-compatible JSON settings model with quick-note hotkey preferences, while keeping Windows startup registration as operating-system state. Add narrow coordinators around global hotkey replacement, desktop-widget runtime preferences, and transactional settings saves; use a modal WPF settings window with code-behind consistent with the existing application.

**Tech Stack:** .NET 8, WPF, Windows Registry, Win32 global hotkeys, WebView2, xUnit, XML/source contract tests

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Run .NET commands serially with `-c Release`.
- Do not add system tray behavior, backup/restore, themes, cloud sync, reminders, language selection, or update checking.
- Keep closing the main window as application exit.
- Keep note Markdown storage, autosave, undo history, quick-note content behavior, and todo data unchanged.
- Use borderless/backgroundless icon buttons with immediate Chinese tooltips.
- Use 8 DIP corner radii for the settings window's text command buttons.

---

### Task 1: Persist and validate quick-note hotkey preferences

**Files:**
- Create: `src/QingJian.App/Hotkeys/QuickNoteHotkeyPreferences.cs`
- Create: `src/QingJian.App/Hotkeys/HotkeyGestureFormatter.cs`
- Create: `tests/QingJian.App.Tests/Hotkeys/QuickNoteHotkeyPreferencesTests.cs`
- Modify: `src/QingJian.App/Hotkeys/HotkeyDefinition.cs`
- Modify: `src/QingJian.App/Services/AppSettingsService.cs`
- Modify: `tests/QingJian.App.Tests/Services/AppSettingsServiceTests.cs`
- Modify: `tests/QingJian.App.Tests/Services/AppSettingsServiceTodoWidgetTests.cs`

**Interfaces:**
- Produces: `QuickNoteHotkeyPreferences(bool IsEnabled, uint Modifiers, uint VirtualKey)`
- Produces: `QuickNoteHotkeyPreferences.Default`, `.Normalize()`, and `.IsValidGesture(uint, uint)`
- Produces: `HotkeyGestureFormatter.Format(uint modifiers, uint virtualKey)`
- Produces: `AppSettings.QuickNoteHotkey`
- Produces: `AppSettingsService.UpdateAsync(Func<AppSettings, AppSettings>, CancellationToken)`

- [ ] **Step 1: Write failing preference, compatibility, and concurrent-update tests**

Cover these exact contracts:

```csharp
[Fact]
public void Normalize_PreservesDisabledStateAndRepairsInvalidGesture()
{
    var value = new QuickNoteHotkeyPreferences(false, 0, 0).Normalize();

    Assert.False(value.IsEnabled);
    Assert.Equal(QuickNoteHotkeyPreferences.Default.Modifiers, value.Modifiers);
    Assert.Equal(QuickNoteHotkeyPreferences.Default.VirtualKey, value.VirtualKey);
}

[Theory]
[InlineData(HotkeyDefinition.ModControl, 0x4E, true)]
[InlineData(HotkeyDefinition.ModAlt | HotkeyDefinition.ModShift, 0x70, true)]
[InlineData(0, 0x4E, false)]
[InlineData(HotkeyDefinition.ModControl, 0x11, false)]
public void IsValidGesture_RequiresModifierAndNonModifierKey(uint modifiers, uint key, bool expected)
{
    Assert.Equal(expected, QuickNoteHotkeyPreferences.IsValidGesture(modifiers, key));
}

[Fact]
public void Format_UsesStableModifierOrder()
{
    Assert.Equal(
        "Ctrl + Alt + Shift + N",
        HotkeyGestureFormatter.Format(
            HotkeyDefinition.ModShift | HotkeyDefinition.ModAlt | HotkeyDefinition.ModControl,
            0x4E));
}
```

Add settings-service tests that an old `{"editorMode":"markdown"}` file loads the default hotkey, saving preserves the todo preferences, and two `UpdateAsync` calls preserve each other's distinct fields.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~QuickNoteHotkeyPreferencesTests|FullyQualifiedName~AppSettingsServiceTests|FullyQualifiedName~AppSettingsServiceTodoWidgetTests"
```

Expected: compilation fails because the hotkey preference types and `UpdateAsync` do not exist.

- [ ] **Step 3: Implement the preference and formatting boundary**

Add modifier constants to `HotkeyDefinition`:

```csharp
public const uint ModShift = 0x0004;
public const uint ModWin = 0x0008;
public const uint AllowedModifiers = ModAlt | ModControl | ModShift | ModWin;
```

Implement `QuickNoteHotkeyPreferences` with default enabled `Ctrl + Alt + N`. Reject modifier virtual keys `0x10`, `0x11`, `0x12`, `0x5B`, and `0x5C`; reject unknown modifier bits and virtual key `0`.

Implement `HotkeyGestureFormatter.Format` in modifier order `Ctrl`, `Alt`, `Shift`, `Win`; format `A-Z`, `0-9`, and `F1-F24` directly, and use `KeyInterop.KeyFromVirtualKey(virtualKey).ToString()` for other valid keys.

- [ ] **Step 4: Extend `AppSettings` without breaking old constructors**

Add `QuickNoteHotkey` to the JSON constructor and keep these overloads working:

```csharp
public AppSettings(string EditorMode)
public AppSettings(string EditorMode, TodoWidgetPreferences TodoWidget)
```

Normalize a missing or invalid hotkey to `QuickNoteHotkeyPreferences.Default`, preserving `IsEnabled` when only the gesture is invalid.

Refactor `AppSettingsService` around private `LoadCoreAsync` and `SaveCoreAsync` methods guarded by one `SemaphoreSlim`. Implement:

```csharp
public Task<AppSettings> UpdateAsync(
    Func<AppSettings, AppSettings> update,
    CancellationToken cancellationToken = default)
```

Make `SaveEditorModeAsync` and `SaveTodoWidgetPreferencesAsync` delegate to `UpdateAsync`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Step 2 command again. Expected: all focused tests pass.

- [ ] **Step 6: Commit Task 1**

```powershell
git add src/QingJian.App/Hotkeys src/QingJian.App/Services/AppSettingsService.cs tests/QingJian.App.Tests/Hotkeys tests/QingJian.App.Tests/Services
git commit -m "feat: persist quick note hotkey settings"
```

### Task 2: Replace the running global hotkey safely

**Files:**
- Create: `src/QingJian.App/Hotkeys/IGlobalHotkeyRegistrar.cs`
- Create: `src/QingJian.App/Hotkeys/QuickNoteHotkeyCoordinator.cs`
- Create: `tests/QingJian.App.Tests/Hotkeys/QuickNoteHotkeyCoordinatorTests.cs`
- Modify: `src/QingJian.App/Hotkeys/GlobalHotkeyService.cs`

**Interfaces:**
- Consumes: `QuickNoteHotkeyPreferences`
- Produces: `IGlobalHotkeyRegistrar.TryRegister(HotkeyDefinition)` and `.Unregister()`
- Produces: `QuickNoteHotkeyCoordinator.Apply(QuickNoteHotkeyPreferences)` returning `bool`
- Produces: `QuickNoteHotkeyCoordinator.CurrentPreferences`

- [ ] **Step 1: Write failing coordinator tests**

Use a fake registrar and cover:

```csharp
[Fact]
public void Apply_ReplacesEnabledGesture()
{
    var registrar = new FakeRegistrar();
    var coordinator = new QuickNoteHotkeyCoordinator(registrar);
    Assert.True(coordinator.Apply(QuickNoteHotkeyPreferences.Default));

    var replacement = new QuickNoteHotkeyPreferences(true, HotkeyDefinition.ModControl, 0x4A);
    Assert.True(coordinator.Apply(replacement));

    Assert.Equal(replacement.Normalize(), coordinator.CurrentPreferences);
    Assert.Equal(1, registrar.UnregisterCount);
}

[Fact]
public void Apply_RestoresPreviousGestureWhenReplacementConflicts()
{
    var registrar = new FakeRegistrar { RejectVirtualKey = 0x4A };
    var coordinator = new QuickNoteHotkeyCoordinator(registrar);
    Assert.True(coordinator.Apply(QuickNoteHotkeyPreferences.Default));

    Assert.False(coordinator.Apply(new(true, HotkeyDefinition.ModControl, 0x4A)));

    Assert.Equal(QuickNoteHotkeyPreferences.Default, coordinator.CurrentPreferences);
    Assert.Equal(QuickNoteHotkeyPreferences.Default.VirtualKey, registrar.Registered!.VirtualKey);
}
```

Also cover disabling unregisters the old gesture and re-enabling registers the saved gesture.

- [ ] **Step 2: Run coordinator tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~QuickNoteHotkeyCoordinatorTests
```

Expected: compilation fails because the coordinator and registrar interface do not exist.

- [ ] **Step 3: Refactor the Win32 service and implement rollback**

Move WPF handle/hook setup into `GlobalHotkeyService.Attach(Window window)`. Implement `IGlobalHotkeyRegistrar` with:

```csharp
bool TryRegister(HotkeyDefinition hotkey);
void Unregister();
event EventHandler? HotkeyPressed;
```

`QuickNoteHotkeyCoordinator.Apply` must normalize and validate first. It unregisters the current enabled gesture, attempts the replacement, and re-registers the previous gesture on conflict. If restoring the previous registration fails, throw `InvalidOperationException("无法恢复原快捷键注册。")`.

- [ ] **Step 4: Run coordinator tests and verify GREEN**

Run the Step 2 command again. Expected: all coordinator tests pass.

- [ ] **Step 5: Commit Task 2**

```powershell
git add src/QingJian.App/Hotkeys tests/QingJian.App.Tests/Hotkeys
git commit -m "feat: reconfigure quick note hotkey"
```

### Task 3: Add Windows startup, storage, and runtime information services

**Files:**
- Create: `src/QingJian.App/Settings/IStartupRegistrationService.cs`
- Create: `src/QingJian.App/Settings/IStartupRegistry.cs`
- Create: `src/QingJian.App/Settings/CurrentUserStartupRegistry.cs`
- Create: `src/QingJian.App/Settings/WindowsStartupRegistrationService.cs`
- Create: `src/QingJian.App/Settings/IAppStorageInfoService.cs`
- Create: `src/QingJian.App/Settings/AppStorageInfoService.cs`
- Create: `src/QingJian.App/Settings/IAppRuntimeInfoProvider.cs`
- Create: `src/QingJian.App/Settings/AppRuntimeInfoProvider.cs`
- Create: `tests/QingJian.App.Tests/Settings/WindowsStartupRegistrationServiceTests.cs`
- Create: `tests/QingJian.App.Tests/Settings/AppStorageInfoServiceTests.cs`
- Create: `tests/QingJian.App.Tests/Settings/AppRuntimeInfoProviderTests.cs`

**Interfaces:**
- Produces: `IStartupRegistrationService.IsEnabled` and `.SetEnabled(bool)`
- Produces: `AppStorageInfo(string DataFolder, long DatabaseBytes, long AttachmentBytes)` with `TotalBytes`
- Produces: `IAppStorageInfoService.GetInfo()` and `.OpenDataFolder()`
- Produces: `IAppRuntimeInfoProvider.GetInfo()`
- Produces: `AppRuntimeInfo(string AppVersion, string DotNetVersion, string WebView2Version)`

- [ ] **Step 1: Write failing startup and storage tests**

Assert that enabling writes value name `QingJian` and command `"C:\Program Files\QingJian\QingJian.App.exe"`, disabling deletes the value, and `IsEnabled` uses the registry value as source of truth.

Create a temporary `qingjian.db`, nested attachment files, and an injected folder launcher. Assert exact byte totals, directory creation, and launcher invocation. Add a file-size reader that throws for one file and assert the remaining files still contribute.

Add runtime-provider tests with injected version delegates: one returns exact application/.NET/WebView2 versions, and one throws while reading WebView2 and expects `未检测到`.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~WindowsStartupRegistrationServiceTests|FullyQualifiedName~AppStorageInfoServiceTests|FullyQualifiedName~AppRuntimeInfoProviderTests"
```

Expected: compilation fails because the settings services do not exist.

- [ ] **Step 3: Implement startup registration**

Use `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` through `CurrentUserStartupRegistry`. `WindowsStartupRegistrationService` receives an executable path and registry abstraction, writes a quoted command, and never requests elevation.

- [ ] **Step 4: Implement robust storage and runtime inspection**

`AppStorageInfoService` implements `IAppStorageInfoService` and receives the app-data folder, optional `Func<string, long>` file-size reader, and optional `Action<string>` launcher. Walk the attachment tree with an explicit directory stack so one inaccessible directory or file can be skipped. Open the folder with `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`.

`AppRuntimeInfoProvider` implements `IAppRuntimeInfoProvider`. It receives optional delegates for testability; production delegates read the entry assembly version, `RuntimeInformation.FrameworkDescription`, and `CoreWebView2Environment.GetAvailableBrowserVersionString()`. Convert any WebView2 exception or empty result to `未检测到`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Step 2 command again. Expected: all startup and storage tests pass.

- [ ] **Step 6: Commit Task 3**

```powershell
git add src/QingJian.App/Settings tests/QingJian.App.Tests/Settings
git commit -m "feat: expose startup and storage settings"
```

### Task 4: Apply desktop todo settings without stale position loss

**Files:**
- Create: `src/QingJian.App/Settings/TodoWidgetSettingsSelection.cs`
- Create: `src/QingJian.App/Settings/TodoWidgetPreferencesMerger.cs`
- Create: `tests/QingJian.App.Tests/Settings/TodoWidgetPreferencesMergerTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetViewModel.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetCoordinator.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetViewModelTests.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetCoordinatorTests.cs`

**Interfaces:**
- Produces: `TodoWidgetSettingsSelection(bool IsVisible, TodoWidgetMode Mode, double Opacity, bool IsLocked, bool ResetPositionAndAppearance)`
- Produces: `TodoWidgetPreferencesMerger.Merge(TodoWidgetPreferences current, TodoWidgetSettingsSelection selection)`
- Produces: `TodoWidgetCoordinator.CapturePreferences()`
- Produces: `TodoWidgetCoordinator.ApplyRuntimePreferencesAsync(TodoWidgetPreferences, CancellationToken)`

- [ ] **Step 1: Write failing merge and runtime-application tests**

Assert normal merge changes only visibility/mode/opacity/lock while preserving live `Left`, `Top`, `CalendarYear`, and `CalendarMonth`. Assert reset uses left/top `80`, mode `EightDay`, opacity `0.75`, and unlocked while preserving the selected visibility and current calendar month.

Add coordinator/view-model tests that runtime application updates mode, opacity, lock, position, and visibility without performing a second settings-file write.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~TodoWidgetPreferencesMergerTests|FullyQualifiedName~TodoWidgetViewModelTests|FullyQualifiedName~TodoWidgetCoordinatorTests"
```

Expected: compilation or assertion failures because merge and runtime apply APIs do not exist.

- [ ] **Step 3: Implement pure merge and view-model application**

Add `TodoWidgetViewModel.ApplyPreferences(TodoWidgetPreferences)` to update mode, lock, opacity, and calendar month through existing notification paths.

`TodoWidgetPreferencesMerger.Merge` must call `Normalize()` and implement reset exactly as specified without changing `IsVisible` or calendar month.

- [ ] **Step 4: Implement coordinator capture and runtime apply**

`CapturePreferences()` uses the current window coordinates and live view model when initialized, otherwise the last loaded settings. `ApplyRuntimePreferencesAsync` updates the coordinator snapshot, view model, window coordinates, and window visibility, loading visible todos when needed; it must not call `AppSettingsService.SaveTodoWidgetPreferencesAsync`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Step 2 command again. Expected: all focused tests pass.

- [ ] **Step 6: Commit Task 4**

```powershell
git add src/QingJian.App/Settings src/QingJian.App/TodoWidgets tests/QingJian.App.Tests/Settings tests/QingJian.App.Tests/TodoWidgets
git commit -m "feat: apply desktop todo settings"
```

### Task 5: Coordinate transactional settings loads and saves

**Files:**
- Create: `src/QingJian.App/Settings/SettingsState.cs`
- Create: `src/QingJian.App/Settings/SettingsSaveRequest.cs`
- Create: `src/QingJian.App/Settings/SettingsSaveResult.cs`
- Create: `src/QingJian.App/Settings/SettingsCoordinator.cs`
- Create: `tests/QingJian.App.Tests/Settings/SettingsCoordinatorTests.cs`

**Interfaces:**
- Consumes: all services from Tasks 1-4
- Produces: `SettingsCoordinator.LoadAsync(CancellationToken)`
- Produces: `SettingsCoordinator.SaveAsync(SettingsSaveRequest, CancellationToken)`
- Produces: Chinese validation/conflict/rollback errors for the settings window

- [ ] **Step 1: Write failing coordinator tests**

Cover these cases with temporary settings and fakes:

1. Load combines JSON settings, actual startup registration, current live todo preferences, storage info, and runtime info.
2. Save validates a required enabled hotkey before side effects.
3. Successful save applies hotkey, startup, JSON, editor callback, and todo runtime preferences.
4. Hotkey conflict returns `快捷键已被其他应用占用，请重新设置。`, applies nothing else, and retains old JSON.
5. Failure after startup change restores startup, old hotkey, old JSON, old editor mode, and old todo runtime preferences.

- [ ] **Step 2: Run coordinator tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~SettingsCoordinatorTests
```

Expected: compilation fails because the settings coordinator contracts do not exist.

- [ ] **Step 3: Implement state, request, result, and save ordering**

Use these records:

```csharp
public sealed record SettingsState(
    AppSettings AppSettings,
    bool LaunchAtStartup,
    TodoWidgetPreferences TodoWidget,
    AppStorageInfo Storage,
    AppRuntimeInfo Runtime);

public sealed record SettingsSaveRequest(
    bool LaunchAtStartup,
    string EditorMode,
    QuickNoteHotkeyPreferences QuickNoteHotkey,
    TodoWidgetSettingsSelection TodoWidget);

public sealed record SettingsSaveResult(bool Succeeded, string? ErrorMessage);
```

Implement the exact save order from the design: validate, apply hotkey, apply startup, update JSON once, apply editor, apply todo. Capture all old state first. On an exception, restore each applied boundary in reverse order and append `请重新启动应用检查设置。` if any rollback operation fails.

- [ ] **Step 4: Run coordinator tests and verify GREEN**

Run the Step 2 command again. Expected: all coordinator tests pass.

- [ ] **Step 5: Commit Task 5**

```powershell
git add src/QingJian.App/Settings tests/QingJian.App.Tests/Settings
git commit -m "feat: coordinate settings updates"
```

### Task 6: Build the modal settings window

**Files:**
- Create: `src/QingJian.App/Settings/SettingsWindow.xaml`
- Create: `src/QingJian.App/Settings/SettingsWindow.xaml.cs`
- Create: `src/QingJian.App/Settings/SettingsWindowDraft.cs`
- Create: `tests/QingJian.App.Tests/Settings/SettingsWindowXamlTests.cs`
- Create: `tests/QingJian.App.Tests/Settings/SettingsWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: `SettingsCoordinator`, `SettingsState`, and `SettingsSaveRequest`
- Produces: six-section 720 x 560 DIP modal settings UI
- Produces: keyboard capture through `HotkeyTextBox_OnPreviewKeyDown`

- [ ] **Step 1: Write failing XAML and code-behind contract tests**

Parse the XAML and assert:

- Window width/min-width `720` and height/min-height `560`.
- Six tab headers in order: `常规`, `编辑`, `快捷便签`, `桌面待办`, `数据`, `关于`.
- Named controls: `LaunchAtStartupCheckBox`, two editor-mode radio buttons, `HotkeyEnabledCheckBox`, `HotkeyTextBox`, three todo-mode radio buttons, `TodoOpacitySlider`, `TodoLockedCheckBox`, `ResetTodoAppearanceButton`, `DataFolderTextBox`, `OpenDataFolderButton`, storage labels, runtime labels, `CancelButton`, `SaveButton`, and `SaveErrorTextBlock`.
- `OpenDataFolderButton` uses an icon, transparent border/background, tooltip `打开数据目录`, and initial delay `0`.
- Cancel/save buttons use 8 DIP corner radius and 8 DIP spacing.

Source tests must assert preview-key capture, `Backspace` clearing, modifier collection, `Esc` cancellation, async save guarding, Chinese errors, and `SettingsSaveRequest` construction.

- [ ] **Step 2: Run window tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~SettingsWindowXamlTests|FullyQualifiedName~SettingsWindowCodeBehindTests"
```

Expected: tests fail because the settings window files do not exist.

- [ ] **Step 3: Implement the fixed settings-window shell**

Create a centered owner modal window with a left `TabControl` strip and unframed right content. Use existing `WindowBackgroundBrush`, `SurfaceBrush`, `PrimaryTextBrush`, `MutedTextBrush`, `BorderBrush`, `AccentBrush`, and `IconButtonStyle` resources. Do not nest cards.

Use checkboxes for binary options, radio-button segmented rows for editor/todo modes, a slider with range `0.35` to `0.95`, and a read-only hotkey text box that still accepts `PreviewKeyDown`.

- [ ] **Step 4: Implement draft loading, capture, data actions, and save**

On load, call `SettingsCoordinator.LoadAsync`, populate a `SettingsWindowDraft`, and render storage sizes with binary units (`B`, `KB`, `MB`, `GB`).

Capture WPF modifiers with `Keyboard.Modifiers`, normalize `Key.System`, convert through `KeyInterop.VirtualKeyFromKey`, ignore modifier-only keys, and update display through `HotkeyGestureFormatter`. `Backspace` clears the draft gesture. `Esc`, cancel, and close do not call save.

On save, prevent duplicate submissions, hide stale errors, call the coordinator, keep the window open on failure, and set `DialogResult = true` only on success.

- [ ] **Step 5: Run window tests and verify GREEN**

Run the Step 2 command again. Expected: all window contract tests pass.

- [ ] **Step 6: Commit Task 6**

```powershell
git add src/QingJian.App/Settings tests/QingJian.App.Tests/Settings
git commit -m "feat: add settings window"
```

### Task 7: Wire the settings entry and application services

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`
- Modify: `src/QingJian.App/App.xaml.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowXamlTests.cs`
- Modify: `tests/QingJian.App.Tests/Views/MainWindowCodeBehindTests.cs`
- Modify: `tests/QingJian.App.Tests/AppStartupWiringTests.cs`

**Interfaces:**
- Produces: `MainWindow.SettingsRequested` event
- Produces: `MainWindow.ApplyEditorModePreferenceAsync(string)`
- Consumes: `SettingsWindow`, `SettingsCoordinator`, startup/storage/runtime services, and hotkey coordinator

- [ ] **Step 1: Write failing wiring tests**

Assert the existing `SettingsButton` has `Click="SettingsButton_OnClick"`; MainWindow raises `SettingsRequested` and exposes the editor-mode application method; App creates the startup, storage, runtime, hotkey, and settings coordinators; App loads hotkey preferences before registration; and App retains one `_settingsWindow` reference, activating it instead of opening a duplicate.

- [ ] **Step 2: Run wiring tests and verify RED**

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowXamlTests|FullyQualifiedName~MainWindowCodeBehindTests|FullyQualifiedName~AppStartupWiringTests"
```

Expected: assertions fail because settings click wiring and application services are absent.

- [ ] **Step 3: Wire MainWindow without changing note workflows**

Add the click handler and `SettingsRequested` event. Implement:

```csharp
public async Task ApplyEditorModePreferenceAsync(string editorMode)
{
    _currentEditorMode = new AppSettings(editorMode).Normalize().EditorMode;
    await SetEditorModeAsync(_currentEditorMode);
    UpdateModeToggleToolTip();
}
```

Do not save inside this method; the settings coordinator owns persistence.

- [ ] **Step 4: Wire application startup and one-window ownership**

Load app settings once before hotkey registration. Attach `GlobalHotkeyService` after `SourceInitialized`, route `HotkeyPressed` to `QuickNoteCoordinator.OpenQuickNote`, and apply the persisted enabled/disabled gesture through `QuickNoteHotkeyCoordinator`.

Create startup registration from `Environment.ProcessPath`, storage from `%LOCALAPPDATA%\QingJian`, runtime info provider, and `SettingsCoordinator`. On `SettingsRequested`, create one owner-assigned `SettingsWindow`; activate an existing visible instance; clear the field after close. Dispose the hotkey service on exit.

Keep the existing Chinese startup warning when initial hotkey registration fails, but show the persisted gesture text.

- [ ] **Step 5: Run wiring tests and verify GREEN**

Run the Step 2 command again. Expected: all wiring tests pass.

- [ ] **Step 6: Commit Task 7**

```powershell
git add src/QingJian.App/App.xaml.cs src/QingJian.App/Views tests/QingJian.App.Tests/AppStartupWiringTests.cs tests/QingJian.App.Tests/Views
git commit -m "feat: wire settings center"
```

### Task 8: Full verification and manual UI check

**Files:**
- Verify: all files changed by Tasks 1-7

**Interfaces:**
- Produces: verified settings center on a clean `feature/ui-polish` worktree

- [ ] **Step 1: Run all tests**

```powershell
dotnet test -c Release
```

Expected: zero failed and zero skipped tests.

- [ ] **Step 2: Build the solution**

```powershell
dotnet build -c Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Launch and manually check the settings workflows**

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj -c Release --no-build
```

Verify: all six sections fit without overlap; cancel leaves settings unchanged; save applies editor and todo changes; hotkey disable/replace works; an occupied hotkey keeps the window open; opening the data directory works; runtime information is visible; and closing the main window still exits.

- [ ] **Step 4: Check branch isolation**

```powershell
git diff --check develop..HEAD
git status --short --branch
git rev-parse develop
git -C C:\Users\Cristin\Desktop\VibeCoding\qingjian status --short --branch
```

Expected: no whitespace errors, clean `feature/ui-polish`, unchanged `develop` at `ba51b89cb6738f0dbb059e37bc40248e4bbd24b6`, and only the pre-existing untracked `outputs/` directory in the main worktree.
