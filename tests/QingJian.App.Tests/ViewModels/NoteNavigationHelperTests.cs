using QingJian.App.Models;
using QingJian.App.ViewModels;
using Xunit;

namespace QingJian.App.Tests.ViewModels;

public sealed class NoteNavigationHelperTests
{
    private static readonly DateTime LocalNow = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void GetSearchMatch_PrioritizesTitleAndIgnoresCase()
    {
        var note = CreateNote("note", "Alpha", "body alpha");

        Assert.Equal(NoteSearchMatchKind.Title, NoteNavigationHelper.GetSearchMatch(note, "ALPHA"));
    }

    [Fact]
    public void GetSearchMatch_ReturnsBodyForBodyOnlyMatch()
    {
        var note = CreateNote("note", "Other", "contains alpha");

        Assert.Equal(NoteSearchMatchKind.Body, NoteNavigationHelper.GetSearchMatch(note, "alpha"));
    }

    [Fact]
    public void GetSearchMatch_ReturnsNoneWhenNothingMatches()
    {
        var note = CreateNote("note", "Other", "body");

        Assert.Equal(NoteSearchMatchKind.None, NoteNavigationHelper.GetSearchMatch(note, "alpha"));
    }

    [Fact]
    public void GetGroupName_UsesSearchGroupsBeforeSortModeGroups()
    {
        var titleMatch = CreateNote("title", "alpha title", "body");
        var bodyMatch = CreateNote("body", "Other", "alpha body");

        Assert.Equal("标题匹配", NoteNavigationHelper.GetGroupName(titleMatch, "alpha", NoteNavigationSortMode.Time, LocalNow));
        Assert.Equal("正文匹配", NoteNavigationHelper.GetGroupName(bodyMatch, "alpha", NoteNavigationSortMode.Favorite, LocalNow));
    }

    [Fact]
    public void GetGroupName_UsesFavoriteAndOtherGroupsWithoutSearch()
    {
        var favorite = CreateNote("favorite", "Favorite", "Body");
        favorite.IsFavorite = true;
        favorite.FavoritedAt = LocalNow.AddMinutes(-5);
        var normal = CreateNote("normal", "Normal", "Body");

        Assert.Equal("收藏", NoteNavigationHelper.GetGroupName(favorite, string.Empty, NoteNavigationSortMode.Favorite, LocalNow));
        Assert.Equal("其他", NoteNavigationHelper.GetGroupName(normal, string.Empty, NoteNavigationSortMode.Favorite, LocalNow));
    }

    [Fact]
    public void Compare_PutsTitleMatchesBeforeBodyMatches()
    {
        var comparer = new NoteNavigationComparer(() => NoteNavigationSortMode.Time, () => "alpha");
        var titleMatch = CreateNote("title", "alpha title", "body", LocalNow.AddHours(-2));
        var bodyMatch = CreateNote("body", "Other", "alpha body", LocalNow.AddHours(-1));

        Assert.True(comparer.Compare(titleMatch, bodyMatch) < 0);
    }

    [Fact]
    public void Compare_FavoriteModeSortsFavoritesByFavoriteTimeBeforeOtherNotes()
    {
        var comparer = new NoteNavigationComparer(() => NoteNavigationSortMode.Favorite, () => string.Empty);
        var olderFavorite = CreateNote("older", "Older", "Body", LocalNow);
        olderFavorite.IsFavorite = true;
        olderFavorite.FavoritedAt = LocalNow.AddHours(-2);
        var newerFavorite = CreateNote("newer", "Newer", "Body", LocalNow.AddHours(-3));
        newerFavorite.IsFavorite = true;
        newerFavorite.FavoritedAt = LocalNow.AddHours(-1);
        var normal = CreateNote("normal", "Normal", "Body", LocalNow.AddHours(1));

        Assert.True(comparer.Compare(newerFavorite, olderFavorite) < 0);
        Assert.True(comparer.Compare(olderFavorite, normal) < 0);
    }

    [Fact]
    public void Compare_SortsNonFavoritesByUpdatedAtDescending()
    {
        var comparer = new NoteNavigationComparer(() => NoteNavigationSortMode.Favorite, () => string.Empty);
        var newer = CreateNote("newer", "Newer", "Body", LocalNow.AddHours(-1));
        var older = CreateNote("older", "Older", "Body", LocalNow.AddHours(-2));

        Assert.True(comparer.Compare(newer, older) < 0);
    }

    [Theory]
    [InlineData("", "0 行 0 字")]
    [InlineData("你好", "1 行 2 字")]
    [InlineData("# 标题", "1 行 2 字")]
    [InlineData("这是 **正文**。", "1 行 6 字")]
    [InlineData("[官网](https://example.com)", "1 行 2 字")]
    [InlineData("![截图](image.png)", "1 行 2 字")]
    [InlineData("- 第一项\n- 第二项", "2 行 6 字")]
    [InlineData("> 引用\n\n`code`", "2 行 6 字")]
    [InlineData("正文 👩‍💻", "1 行 4 字")]
    public void FormatBodyStats_CountsRenderedPlainText(string markdown, string expected)
    {
        Assert.Equal(expected, NoteNavigationHelper.FormatBodyStats(markdown));
    }

    private static Note CreateNote(string id, string title, string content, DateTime? updatedAt = null)
    {
        var timestamp = updatedAt ?? LocalNow;
        return new Note
        {
            Id = id,
            Title = title,
            Content = content,
            CreatedAt = timestamp.AddMinutes(-1),
            UpdatedAt = timestamp,
            FolderName = "未分类",
            IsDeleted = false
        };
    }
}
