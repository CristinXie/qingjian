using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QingJian.App.Hotkeys;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public sealed record AppSettings
{
    public const string DefaultEditorMode = "wysiwyg";
    public const string MarkdownEditorMode = "markdown";

    public static AppSettings Default { get; } = new(
        DefaultEditorMode,
        TodoWidgetPreferences.Default,
        QuickNoteHotkeyPreferences.Default);

    [JsonConstructor]
    public AppSettings(
        string? EditorMode,
        TodoWidgetPreferences? TodoWidget,
        QuickNoteHotkeyPreferences? QuickNoteHotkey)
    {
        this.EditorMode = EditorMode ?? DefaultEditorMode;
        this.TodoWidget = TodoWidget ?? TodoWidgetPreferences.Default;
        this.QuickNoteHotkey = QuickNoteHotkey ?? QuickNoteHotkeyPreferences.Default;
    }

    public AppSettings(string EditorMode, TodoWidgetPreferences TodoWidget)
        : this(EditorMode, TodoWidget, QuickNoteHotkeyPreferences.Default)
    {
    }

    public AppSettings(string EditorMode)
        : this(EditorMode, TodoWidgetPreferences.Default, QuickNoteHotkeyPreferences.Default)
    {
    }

    public string EditorMode { get; init; }

    public TodoWidgetPreferences TodoWidget { get; init; }

    public QuickNoteHotkeyPreferences QuickNoteHotkey { get; init; }

    public AppSettings Normalize()
    {
        var editorMode = EditorMode is DefaultEditorMode or MarkdownEditorMode
            ? EditorMode
            : DefaultEditorMode;

        return this with
        {
            EditorMode = editorMode,
            TodoWidget = (TodoWidget ?? TodoWidgetPreferences.Default).Normalize(),
            QuickNoteHotkey = (QuickNoteHotkey ?? QuickNoteHotkeyPreferences.Default).Normalize()
        };
    }
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _settingsPath;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);

    public AppSettingsService(string appDataFolder)
    {
        _settingsPath = Path.Combine(appDataFolder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _settingsGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task<AppSettings> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return (settings ?? AppSettings.Default).Normalize();
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task SaveCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var normalizedSettings = settings.Normalize();

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer
            .SerializeAsync(stream, normalizedSettings, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AppSettings> UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _settingsGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var currentSettings = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            var updatedSettings = update(currentSettings).Normalize();
            await SaveCoreAsync(updatedSettings, cancellationToken).ConfigureAwait(false);
            return updatedSettings;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    public async Task<AppSettings> SaveEditorModeAsync(string editorMode, CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(
            settings => settings with { EditorMode = editorMode },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppSettings> SaveTodoWidgetPreferencesAsync(
        TodoWidgetPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        return await UpdateAsync(
            settings => settings with { TodoWidget = preferences },
            cancellationToken).ConfigureAwait(false);
    }
}
