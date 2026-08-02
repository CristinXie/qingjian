using QingJian.App.Hotkeys;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Settings;

public sealed class SettingsWindowDraft
{
    public bool LaunchAtStartup { get; set; }

    public WindowBehaviorPreferences WindowBehavior { get; set; } = WindowBehaviorPreferences.Default;

    public string EditorMode { get; set; } = AppSettings.DefaultEditorMode;

    public QuickNoteHotkeyPreferences QuickNoteHotkey { get; set; } = QuickNoteHotkeyPreferences.Default;

    public bool TodoVisible { get; set; }

    public TodoWidgetMode TodoMode { get; set; } = TodoWidgetMode.EightDay;

    public double TodoOpacity { get; set; } = TodoWidgetPreferences.Default.Opacity;

    public bool TodoLocked { get; set; }

    public bool ResetTodoPositionAndAppearance { get; set; }
}
