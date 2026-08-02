using QingJian.App.Hotkeys;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Settings;

public sealed class SettingsCoordinator
{
    private const string HotkeyConflictMessage = "快捷键已被其他应用占用，请重新设置。";
    private const string InvalidHotkeyMessage = "请选择有效的快捷键。";
    private const string RollbackFailureMessage = "请重新启动应用检查设置。";

    private readonly AppSettingsService _settingsService;
    private readonly IStartupRegistrationService _startupService;
    private readonly QuickNoteHotkeyCoordinator _hotkeyCoordinator;
    private readonly TodoWidgetCoordinator _todoWidgetCoordinator;
    private readonly IAppStorageInfoService _storageService;
    private readonly IAppRuntimeInfoProvider _runtimeInfoProvider;
    private readonly Func<string, Task> _applyEditorMode;
    private readonly Func<WindowBehaviorPreferences, Task> _applyWindowBehavior;

    public SettingsCoordinator(
        AppSettingsService settingsService,
        IStartupRegistrationService startupService,
        QuickNoteHotkeyCoordinator hotkeyCoordinator,
        TodoWidgetCoordinator todoWidgetCoordinator,
        IAppStorageInfoService storageService,
        IAppRuntimeInfoProvider runtimeInfoProvider,
        Func<string, Task> applyEditorMode)
        : this(
            settingsService,
            startupService,
            hotkeyCoordinator,
            todoWidgetCoordinator,
            storageService,
            runtimeInfoProvider,
            applyEditorMode,
            _ => Task.CompletedTask)
    {
    }

    public SettingsCoordinator(
        AppSettingsService settingsService,
        IStartupRegistrationService startupService,
        QuickNoteHotkeyCoordinator hotkeyCoordinator,
        TodoWidgetCoordinator todoWidgetCoordinator,
        IAppStorageInfoService storageService,
        IAppRuntimeInfoProvider runtimeInfoProvider,
        Func<string, Task> applyEditorMode,
        Func<WindowBehaviorPreferences, Task> applyWindowBehavior)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _hotkeyCoordinator = hotkeyCoordinator;
        _todoWidgetCoordinator = todoWidgetCoordinator;
        _storageService = storageService;
        _runtimeInfoProvider = runtimeInfoProvider;
        _applyEditorMode = applyEditorMode;
        _applyWindowBehavior = applyWindowBehavior;
    }

    public async Task<SettingsState> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        return new SettingsState(
            settings,
            _startupService.IsEnabled,
            _todoWidgetCoordinator.CapturePreferences(),
            _storageService.GetInfo(),
            _runtimeInfoProvider.GetInfo());
    }

    public void OpenDataFolder()
    {
        _storageService.OpenDataFolder();
    }

    public async Task<SettingsSaveResult> SaveAsync(
        SettingsSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.QuickNoteHotkey.IsEnabled && !QuickNoteHotkeyPreferences.IsValidGesture(
                request.QuickNoteHotkey.Modifiers,
                request.QuickNoteHotkey.VirtualKey))
        {
            return SettingsSaveResult.Failure(InvalidHotkeyMessage);
        }

        var oldSettings = await _settingsService.LoadAsync(cancellationToken);
        var oldStartup = _startupService.IsEnabled;
        var oldHotkey = _hotkeyCoordinator.CurrentPreferences;
        var oldTodo = _todoWidgetCoordinator.CapturePreferences();
        var editorMode = new AppSettings(request.EditorMode).Normalize().EditorMode;
        var hotkey = !request.QuickNoteHotkey.IsEnabled &&
            !QuickNoteHotkeyPreferences.IsValidGesture(
                request.QuickNoteHotkey.Modifiers,
                request.QuickNoteHotkey.VirtualKey)
            ? oldSettings.QuickNoteHotkey.Normalize() with { IsEnabled = false }
            : request.QuickNoteHotkey.Normalize();
        var todo = TodoWidgetPreferencesMerger.Merge(oldTodo, request.TodoWidget);
        var windowBehavior = request.WindowBehavior ?? oldSettings.WindowBehavior;
        var hotkeyTouched = false;
        var startupTouched = false;
        var settingsTouched = false;

        try
        {
            hotkeyTouched = true;
            if (!_hotkeyCoordinator.Apply(hotkey))
            {
                return SettingsSaveResult.Failure(HotkeyConflictMessage);
            }

            if (oldStartup != request.LaunchAtStartup)
            {
                startupTouched = true;
                _startupService.SetEnabled(request.LaunchAtStartup);
            }

            settingsTouched = true;
            await _settingsService.UpdateAsync(
                settings => settings with
                {
                    EditorMode = editorMode,
                    QuickNoteHotkey = hotkey,
                    TodoWidget = todo,
                    WindowBehavior = windowBehavior
                },
                cancellationToken);

            await _applyWindowBehavior(windowBehavior);
            await _applyEditorMode(editorMode);
            await _todoWidgetCoordinator.ApplyRuntimePreferencesAsync(todo, cancellationToken);
            return SettingsSaveResult.Success();
        }
        catch (Exception ex)
        {
            var rollbackFailed = await RollbackAsync(
                oldSettings,
                oldStartup,
                oldHotkey,
                oldTodo,
                hotkeyTouched,
                startupTouched,
                settingsTouched);
            var message = $"保存设置失败：{ex.Message}";
            if (rollbackFailed)
            {
                message = $"{message} {RollbackFailureMessage}";
            }

            return SettingsSaveResult.Failure(message);
        }
    }

    private async Task<bool> RollbackAsync(
        AppSettings oldSettings,
        bool oldStartup,
        QuickNoteHotkeyPreferences oldHotkey,
        TodoWidgetPreferences oldTodo,
        bool hotkeyTouched,
        bool startupTouched,
        bool settingsTouched)
    {
        var failed = false;

        if (settingsTouched)
        {
            failed |= !await TryRollbackAsync(() => _todoWidgetCoordinator.ApplyRuntimePreferencesAsync(oldTodo));
            failed |= !await TryRollbackAsync(() => _applyEditorMode(oldSettings.EditorMode));
            failed |= !await TryRollbackAsync(() => _applyWindowBehavior(oldSettings.WindowBehavior));
            failed |= !await TryRollbackAsync(() => _settingsService.SaveAsync(oldSettings));
        }

        if (startupTouched)
        {
            failed |= !TryRollback(() => _startupService.SetEnabled(oldStartup));
        }

        if (hotkeyTouched)
        {
            failed |= !TryRollback(() =>
            {
                if (!_hotkeyCoordinator.Apply(oldHotkey))
                {
                    throw new InvalidOperationException("无法恢复原快捷键。");
                }
            });
        }

        return failed;
    }

    private static async Task<bool> TryRollbackAsync(Func<Task> rollback)
    {
        try
        {
            await rollback();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryRollback(Action rollback)
    {
        try
        {
            rollback();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
