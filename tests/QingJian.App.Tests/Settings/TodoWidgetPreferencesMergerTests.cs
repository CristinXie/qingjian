using QingJian.App.Settings;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class TodoWidgetPreferencesMergerTests
{
    [Fact]
    public void Merge_ChangesOnlySelectableValues()
    {
        var current = TodoWidgetPreferences.Default with
        {
            Left = 320,
            Top = 180,
            CalendarYear = 2027,
            CalendarMonth = 4
        };
        var selection = new TodoWidgetSettingsSelection(
            IsVisible: false,
            Mode: TodoWidgetMode.Today,
            Opacity: 0.52,
            IsLocked: true,
            ResetPositionAndAppearance: false);

        var merged = TodoWidgetPreferencesMerger.Merge(current, selection);

        Assert.False(merged.IsVisible);
        Assert.Equal(TodoWidgetMode.Today, merged.Mode);
        Assert.Equal(0.52, merged.Opacity);
        Assert.True(merged.IsLocked);
        Assert.Equal(320, merged.Left);
        Assert.Equal(180, merged.Top);
        Assert.Equal(2027, merged.CalendarYear);
        Assert.Equal(4, merged.CalendarMonth);
    }

    [Fact]
    public void Merge_ResetRestoresPositionAndAppearanceWithoutChangingVisibilityOrMonth()
    {
        var current = TodoWidgetPreferences.Default with
        {
            Left = 320,
            Top = 180,
            Mode = TodoWidgetMode.Calendar,
            Opacity = 0.45,
            IsLocked = true,
            CalendarYear = 2027,
            CalendarMonth = 4
        };
        var selection = new TodoWidgetSettingsSelection(
            IsVisible: false,
            Mode: TodoWidgetMode.Today,
            Opacity: 0.52,
            IsLocked: true,
            ResetPositionAndAppearance: true);

        var merged = TodoWidgetPreferencesMerger.Merge(current, selection);

        Assert.False(merged.IsVisible);
        Assert.Equal(TodoWidgetMode.EightDay, merged.Mode);
        Assert.Equal(0.75, merged.Opacity);
        Assert.False(merged.IsLocked);
        Assert.Equal(80, merged.Left);
        Assert.Equal(80, merged.Top);
        Assert.Equal(2027, merged.CalendarYear);
        Assert.Equal(4, merged.CalendarMonth);
    }
}
