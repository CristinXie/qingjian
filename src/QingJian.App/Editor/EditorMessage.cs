using System.Text.Json;

namespace QingJian.App.Editor;

public readonly record struct EditorMessage(string Type, string NoteId, string Markdown, string Url)
{
    public const string MarkdownChangedType = "markdownChanged";
    public const string ExternalLinkRequestedType = "externalLinkRequested";

    public static EditorMessage Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

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

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String ||
                typeElement.GetString() is not { } type ||
                type is not (MarkdownChangedType or ExternalLinkRequestedType))
            {
                return false;
            }

            if (type == ExternalLinkRequestedType)
            {
                return TryParseExternalLink(root, type, out message);
            }

            var noteId = string.Empty;
            if (root.TryGetProperty("noteId", out var noteIdElement))
            {
                if (noteIdElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                noteId = noteIdElement.GetString() ?? string.Empty;
            }

            var markdown = string.Empty;
            if (root.TryGetProperty("markdown", out var markdownElement))
            {
                if (markdownElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                markdown = markdownElement.GetString() ?? string.Empty;
            }

            message = new EditorMessage(type, noteId, markdown, string.Empty);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseExternalLink(JsonElement root, string type, out EditorMessage message)
    {
        message = Empty;

        if (!root.TryGetProperty("url", out var urlElement) ||
            urlElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(urlElement.GetString()))
        {
            return false;
        }

        message = new EditorMessage(type, string.Empty, string.Empty, urlElement.GetString()!);
        return true;
    }
}
