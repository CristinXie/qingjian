using System.Text.Json;

namespace QingJian.App.Editor;

public readonly record struct EditorMessage(string Type, string NoteId, string Markdown)
{
    public const string MarkdownChangedType = "markdownChanged";

    public static EditorMessage Empty { get; } = new(string.Empty, string.Empty, string.Empty);

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
                type != MarkdownChangedType)
            {
                return false;
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

            message = new EditorMessage(type, noteId, markdown);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
