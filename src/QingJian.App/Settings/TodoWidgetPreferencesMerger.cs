using QingJian.App.TodoWidgets;

namespace QingJian.App.Settings;

public static class TodoWidgetPreferencesMerger
{
    public static TodoWidgetPreferences Merge(
        TodoWidgetPreferences current,
        TodoWidgetSettingsSelection selection)
    {
        var normalizedCurrent = current.Normalize();
        if (selection.ResetPositionAndAppearance)
        {
            return normalizedCurrent with
            {
                IsVisible = selection.IsVisible,
                Mode = TodoWidgetPreferences.Default.Mode,
                Left = TodoWidgetPreferences.Default.Left,
                Top = TodoWidgetPreferences.Default.Top,
                Opacity = TodoWidgetPreferences.Default.Opacity,
                IsLocked = TodoWidgetPreferences.Default.IsLocked
            };
        }

        return (normalizedCurrent with
        {
            IsVisible = selection.IsVisible,
            Mode = selection.Mode,
            Opacity = selection.Opacity,
            IsLocked = selection.IsLocked
        }).Normalize();
    }
}
