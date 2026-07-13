using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QingJian.App.TodoWidgets;

namespace QingJian.App.Services;

public sealed record AppSettings
{
    public const string DefaultEditorMode = "wysiwyg";
    public const string MarkdownEditorMode = "markdown";

    public static AppSettings Default { get; } = new(DefaultEditorMode, TodoWidgetPreferences.Default);

    [JsonConstructor]
    public AppSettings(string EditorMode, TodoWidgetPreferences TodoWidget)
    {
        this.EditorMode = EditorMode;
        this.TodoWidget = TodoWidget;
    }

    public AppSettings(string EditorMode)
        : this(EditorMode, TodoWidgetPreferences.Default)
    {
    }

    public string EditorMode { get; init; }

    public TodoWidgetPreferences TodoWidget { get; init; }

    public AppSettings Normalize()
    {
        var editorMode = EditorMode is DefaultEditorMode or MarkdownEditorMode
            ? EditorMode
            : DefaultEditorMode;

        return this with
        {
            EditorMode = editorMode,
            TodoWidget = (TodoWidget ?? TodoWidgetPreferences.Default).Normalize()
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

    public AppSettingsService(string appDataFolder)
    {
        _settingsPath = Path.Combine(appDataFolder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            return (settings ?? AppSettings.Default).Normalize();
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var normalizedSettings = settings.Normalize();

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, normalizedSettings, JsonOptions, cancellationToken);
    }
}
