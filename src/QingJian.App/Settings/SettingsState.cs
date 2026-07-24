using QingJian.App.Services;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Settings;

public sealed record SettingsState(
    AppSettings AppSettings,
    bool LaunchAtStartup,
    TodoWidgetPreferences TodoWidget,
    AppStorageInfo Storage,
    AppRuntimeInfo Runtime);
