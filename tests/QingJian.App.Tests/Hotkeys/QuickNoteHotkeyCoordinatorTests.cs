using QingJian.App.Hotkeys;
using Xunit;

namespace QingJian.App.Tests.Hotkeys;

public sealed class QuickNoteHotkeyCoordinatorTests
{
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
        Assert.Equal(0x4Au, registrar.Registered!.VirtualKey);
    }

    [Fact]
    public void Apply_RestoresPreviousGestureWhenReplacementConflicts()
    {
        var registrar = new FakeRegistrar { RejectVirtualKey = 0x4A };
        var coordinator = new QuickNoteHotkeyCoordinator(registrar);
        Assert.True(coordinator.Apply(QuickNoteHotkeyPreferences.Default));

        Assert.False(coordinator.Apply(new QuickNoteHotkeyPreferences(
            true,
            HotkeyDefinition.ModControl,
            0x4A)));

        Assert.Equal(QuickNoteHotkeyPreferences.Default, coordinator.CurrentPreferences);
        Assert.Equal(QuickNoteHotkeyPreferences.Default.VirtualKey, registrar.Registered!.VirtualKey);
    }

    [Fact]
    public void Apply_DisablesAndReenablesConfiguredGesture()
    {
        var registrar = new FakeRegistrar();
        var coordinator = new QuickNoteHotkeyCoordinator(registrar);
        var enabled = new QuickNoteHotkeyPreferences(true, HotkeyDefinition.ModControl, 0x4A);
        Assert.True(coordinator.Apply(enabled));

        Assert.True(coordinator.Apply(enabled with { IsEnabled = false }));

        Assert.Null(registrar.Registered);
        Assert.False(coordinator.CurrentPreferences.IsEnabled);

        Assert.True(coordinator.Apply(enabled));

        Assert.Equal(enabled, coordinator.CurrentPreferences);
        Assert.Equal(0x4Au, registrar.Registered!.VirtualKey);
    }

    [Fact]
    public void Apply_ThrowsWhenPreviousGestureCannotBeRestored()
    {
        var registrar = new FakeRegistrar();
        var coordinator = new QuickNoteHotkeyCoordinator(registrar);
        Assert.True(coordinator.Apply(QuickNoteHotkeyPreferences.Default));
        registrar.RejectAll = true;

        var exception = Assert.Throws<InvalidOperationException>(() => coordinator.Apply(
            new QuickNoteHotkeyPreferences(true, HotkeyDefinition.ModControl, 0x4A)));

        Assert.Equal("无法恢复原快捷键注册。", exception.Message);
    }

    private sealed class FakeRegistrar : IGlobalHotkeyRegistrar
    {
        public event EventHandler? HotkeyPressed;

        public uint? RejectVirtualKey { get; init; }

        public bool RejectAll { get; set; }

        public HotkeyDefinition? Registered { get; private set; }

        public int UnregisterCount { get; private set; }

        public bool TryRegister(HotkeyDefinition hotkey)
        {
            if (RejectAll || hotkey.VirtualKey == RejectVirtualKey)
            {
                return false;
            }

            Registered = hotkey;
            return true;
        }

        public void Unregister()
        {
            UnregisterCount++;
            Registered = null;
        }

        public void RaiseHotkeyPressed()
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }
}
