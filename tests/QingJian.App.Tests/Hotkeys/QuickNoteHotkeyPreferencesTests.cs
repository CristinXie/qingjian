using QingJian.App.Hotkeys;
using Xunit;

namespace QingJian.App.Tests.Hotkeys;

public sealed class QuickNoteHotkeyPreferencesTests
{
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
    [InlineData(0x1000, 0x4E, false)]
    public void IsValidGesture_RequiresModifierAndNonModifierKey(uint modifiers, uint key, bool expected)
    {
        Assert.Equal(expected, QuickNoteHotkeyPreferences.IsValidGesture(modifiers, key));
    }

    [Fact]
    public void Format_UsesStableModifierOrder()
    {
        var text = HotkeyGestureFormatter.Format(
            HotkeyDefinition.ModShift | HotkeyDefinition.ModAlt | HotkeyDefinition.ModControl,
            0x4E);

        Assert.Equal("Ctrl + Alt + Shift + N", text);
    }

    [Fact]
    public void Format_FormatsFunctionKeys()
    {
        var text = HotkeyGestureFormatter.Format(HotkeyDefinition.ModControl, 0x70);

        Assert.Equal("Ctrl + F1", text);
    }
}
