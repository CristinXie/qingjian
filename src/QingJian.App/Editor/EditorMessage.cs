using System.Text.Json;

namespace QingJian.App.Editor;

public readonly record struct EditorMessage(string Type, string Markdown)
{
    public const string MarkdownChangedType = "markdownChanged";

    public static EditorMessage Empty { get; } = new(string.Empty, string.Empty);

    public static bool TryParse(string? json, out EditorMessage message)
    {
        message = Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement) ||
                typeElement.GetString() is not { } type ||
                type != MarkdownChangedType)
            {
                return false;
            }

            var markdown = root.TryGetProperty("markdown", out var markdownElement)
                ? markdownElement.GetString() ?? string.Empty
                : string.Empty;

            message = new EditorMessage(type, markdown);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
