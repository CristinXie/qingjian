using QingJian.App.TodoWidgets;

namespace QingJian.App.Settings;

public sealed record TodoWidgetSettingsSelection(
    bool IsVisible,
    TodoWidgetMode Mode,
    double Opacity,
    bool IsLocked,
    bool ResetPositionAndAppearance);
