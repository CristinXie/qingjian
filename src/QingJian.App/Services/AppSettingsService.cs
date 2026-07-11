using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QingJian.App.Services;

public sealed record AppSettings(string EditorMode)
{
    public const string DefaultEditorMode = "wysiwyg";
    public const string MarkdownEditorMode = "markdown";

    public static AppSettings Default { get; } = new(DefaultEditorMode);

    public AppSettings Normalize()
    {
        return EditorMode is DefaultEditorMode or MarkdownEditorMode
            ? this
            : Default;
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
