namespace QingJian.App.QuickNotes;

public static class QuickNoteTitleGenerator
{
    public const int MaxTitleLength = 40;

    public static bool HasBody(string? body)
    {
        return !string.IsNullOrWhiteSpace(body);
    }

    public static string CreateTitle(string? requestedTitle, string body)
    {
        var title = string.IsNullOrWhiteSpace(requestedTitle)
            ? FirstBodyLine(body)
            : requestedTitle.Trim();

        return Truncate(title);
    }

    private static string FirstBodyLine(string body)
    {
        var lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return lines.Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? string.Empty;
    }

    private static string Truncate(string title)
    {
        if (title.Length <= MaxTitleLength)
        {
            return title;
        }

        return string.Concat(title.AsSpan(0, MaxTitleLength - 3), "...");
    }
}
