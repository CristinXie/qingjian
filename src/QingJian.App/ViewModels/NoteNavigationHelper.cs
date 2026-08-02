using System.Globalization;
using System.IO;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public static class NoteNavigationHelper
{
    private const string EmptyEditorLineMarker = "\uE000";

    private static readonly MarkdownPipeline BodyStatsPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static NoteSearchMatchKind GetSearchMatch(Note note, string? searchText)
    {
        var query = searchText?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return NoteSearchMatchKind.None;
        }

        if (note.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return NoteSearchMatchKind.Title;
        }

        return note.Content.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            ? NoteSearchMatchKind.Body
            : NoteSearchMatchKind.None;
    }

    public static string GetGroupName(
        Note note,
        string? searchText,
        NoteNavigationSortMode sortMode,
        DateTime localNow)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            return GetSearchMatch(note, searchText) switch
            {
                NoteSearchMatchKind.Title => "标题匹配",
                NoteSearchMatchKind.Body => "正文匹配",
                _ => string.Empty
            };
        }

        if (sortMode == NoteNavigationSortMode.Favorite)
        {
            return note.IsFavorite ? "收藏" : "其他";
        }

        return NoteNavigationDateHelper.GetGroupName(note.UpdatedAt, localNow);
    }

    public static string FormatBodyStats(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return "0 行 0 字";
        }

        var plainText = RenderPlainText(markdown).Replace("\r\n", "\n").Replace('\r', '\n');
        var normalized = plainText.TrimEnd('\n');
        if (normalized.Length == 0)
        {
            return "0 行 0 字";
        }

        var lineCount = 1;
        var characterCount = 0;

        var textElements = StringInfo.GetTextElementEnumerator(normalized);
        while (textElements.MoveNext())
        {
            if (textElements.GetTextElement() == "\n")
            {
                lineCount++;
            }
            else if (textElements.GetTextElement() != EmptyEditorLineMarker)
            {
                characterCount++;
            }
        }

        return $"{lineCount} 行 {characterCount} 字";
    }

    private static string RenderPlainText(string markdown)
    {
        var document = Markdown.Parse(markdown, BodyStatsPipeline);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer)
        {
            EnableHtmlForBlock = false,
            EnableHtmlForInline = false,
            EnableHtmlEscape = false
        };

        BodyStatsPipeline.Setup(renderer);
        renderer.ObjectRenderers.TryRemove<HtmlBlockRenderer>();
        renderer.ObjectRenderers.TryRemove<HtmlInlineRenderer>();
        renderer.ObjectRenderers.Add(new PlainTextHtmlBlockRenderer());
        renderer.ObjectRenderers.Add(new PlainTextHtmlInlineRenderer());
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    private static bool IsBreakTag(string value)
    {
        var tag = value.Trim();
        return tag.Equals("<br>", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("<br/>", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("<br />", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PlainTextHtmlBlockRenderer : HtmlObjectRenderer<HtmlBlock>
    {
        protected override void Write(HtmlRenderer renderer, HtmlBlock block)
        {
            var blockText = block.Lines.ToString().Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
            var lines = blockText.Split('\n');
            if (lines.Length == 0 || !lines.All(IsBreakTag))
            {
                renderer.WriteLeafRawLines(block, true, false);
                return;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (index > 0)
                {
                    renderer.WriteLine();
                }

                renderer.Write(EmptyEditorLineMarker);
            }

            renderer.EnsureLine();
        }
    }

    private sealed class PlainTextHtmlInlineRenderer : HtmlObjectRenderer<HtmlInline>
    {
        protected override void Write(HtmlRenderer renderer, HtmlInline inline)
        {
            if (IsBreakTag(inline.Tag))
            {
                renderer.WriteLine();
            }
        }
    }
}
