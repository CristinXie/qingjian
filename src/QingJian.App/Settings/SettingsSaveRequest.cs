using QingJian.App.Hotkeys;

namespace QingJian.App.Settings;

public sealed record SettingsSaveRequest(
    bool LaunchAtStartup,
    string EditorMode,
    QuickNoteHotkeyPreferences QuickNoteHotkey,
    TodoWidgetSettingsSelection TodoWidget,
    WindowBehaviorPreferences? WindowBehavior = null);
