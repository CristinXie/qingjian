namespace QingJian.App.Hotkeys;

public sealed class QuickNoteHotkeyCoordinator
{
    private readonly IGlobalHotkeyRegistrar _registrar;
    private bool _hasAppliedPreferences;

    public QuickNoteHotkeyCoordinator(IGlobalHotkeyRegistrar registrar)
    {
        _registrar = registrar;
    }

    public QuickNoteHotkeyPreferences CurrentPreferences { get; private set; } =
        QuickNoteHotkeyPreferences.Default with { IsEnabled = false };

    public bool Apply(QuickNoteHotkeyPreferences preferences)
    {
        var normalized = preferences.Normalize();
        if (_hasAppliedPreferences && normalized == CurrentPreferences)
        {
            return true;
        }

        var previous = CurrentPreferences;
        var previousWasApplied = _hasAppliedPreferences;
        if (previousWasApplied && previous.IsEnabled)
        {
            _registrar.Unregister();
        }

        if (!normalized.IsEnabled)
        {
            CurrentPreferences = normalized;
            _hasAppliedPreferences = true;
            return true;
        }

        if (_registrar.TryRegister(CreateDefinition(normalized)))
        {
            CurrentPreferences = normalized;
            _hasAppliedPreferences = true;
            return true;
        }

        if (previousWasApplied && previous.IsEnabled &&
            !_registrar.TryRegister(CreateDefinition(previous)))
        {
            throw new InvalidOperationException("无法恢复原快捷键注册。");
        }

        return false;
    }

    private static HotkeyDefinition CreateDefinition(QuickNoteHotkeyPreferences preferences)
    {
        return new HotkeyDefinition(
            HotkeyDefinition.QuickNoteHotkey.Id,
            preferences.Modifiers,
            preferences.VirtualKey,
            HotkeyGestureFormatter.Format(preferences.Modifiers, preferences.VirtualKey));
    }
}
