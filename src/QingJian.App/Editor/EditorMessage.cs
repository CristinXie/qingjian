using System.Text.Json;

namespace QingJian.App.Editor;

public readonly record struct EditorMessage(
    string Type,
    string NoteId,
    string Markdown,
    string Url,
    string RequestId,
    string FileName,
    string DataUrl,
    string EditorMode)
{
    public const string MarkdownChangedType = "markdownChanged";
    public const string ExternalLinkRequestedType = "externalLinkRequested";
    public const string LocalImageRequestedType = "localImageRequested";
    public const string EditorModeChangedType = "editorModeChanged";

    public static EditorMessage Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);

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
                type is not (MarkdownChangedType or ExternalLinkRequestedType or LocalImageRequestedType or EditorModeChangedType))
            {
                return false;
            }

            if (type == ExternalLinkRequestedType)
            {
                return TryParseExternalLink(root, type, out message);
            }

            if (type == LocalImageRequestedType)
            {
                return TryParseLocalImage(root, type, out message);
            }

            if (type == EditorModeChangedType)
            {
                return TryParseEditorMode(root, type, out message);
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

            message = new EditorMessage(type, noteId, markdown, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
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

        message = new EditorMessage(
            type,
            string.Empty,
            string.Empty,
            urlElement.GetString()!,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        return true;
    }

    private static bool TryParseLocalImage(JsonElement root, string type, out EditorMessage message)
    {
        message = Empty;

        if (!TryGetRequiredString(root, "requestId", out var requestId) ||
            !TryGetRequiredString(root, "fileName", out var fileName) ||
            !TryGetRequiredString(root, "dataUrl", out var dataUrl))
        {
            return false;
        }

        message = new EditorMessage(
            type,
            string.Empty,
            string.Empty,
            string.Empty,
            requestId,
            fileName,
            dataUrl,
            string.Empty);
        return true;
    }

    private static bool TryParseEditorMode(JsonElement root, string type, out EditorMessage message)
    {
        message = Empty;

        if (!TryGetRequiredString(root, "editorMode", out var editorMode) ||
            editorMode is not ("wysiwyg" or "markdown"))
        {
            return false;
        }

        message = new EditorMessage(
            type,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            editorMode);
        return true;
    }

    private static bool TryGetRequiredString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;

        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            return false;
        }

        value = element.GetString()!;
        return true;
    }
}
