using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public static class NoteNavigationHelper
{
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

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lineCount = 1;
        var characterCount = 0;

        foreach (var character in normalized)
        {
            if (character == '\n')
            {
                lineCount++;
            }
            else
            {
                characterCount++;
            }
        }

        return $"{lineCount} 行 {characterCount} 字";
    }
}
