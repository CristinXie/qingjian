using System.Collections;
using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public sealed class NoteNavigationComparer : IComparer
{
    private readonly Func<NoteNavigationSortMode> _sortMode;
    private readonly Func<string> _searchText;

    public NoteNavigationComparer(Func<NoteNavigationSortMode> sortMode, Func<string> searchText)
    {
        _sortMode = sortMode;
        _searchText = searchText;
    }

    public int Compare(object? x, object? y)
    {
        return Compare(x as Note, y as Note);
    }

    public int Compare(Note? x, Note? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var searchText = _searchText();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var matchComparison = GetMatchRank(x, searchText).CompareTo(GetMatchRank(y, searchText));
            if (matchComparison != 0)
            {
                return matchComparison;
            }
        }

        if (_sortMode() == NoteNavigationSortMode.Favorite)
        {
            var favoriteComparison = GetFavoriteRank(x).CompareTo(GetFavoriteRank(y));
            if (favoriteComparison != 0)
            {
                return favoriteComparison;
            }

            if (x.IsFavorite && y.IsFavorite)
            {
                var favoriteTimeComparison = Nullable.Compare(y.FavoritedAt, x.FavoritedAt);
                if (favoriteTimeComparison != 0)
                {
                    return favoriteTimeComparison;
                }
            }
        }

        var updatedComparison = y.UpdatedAt.CompareTo(x.UpdatedAt);
        return updatedComparison != 0
            ? updatedComparison
            : string.Compare(x.Id, y.Id, StringComparison.Ordinal);
    }

    private static int GetMatchRank(Note note, string searchText)
    {
        return NoteNavigationHelper.GetSearchMatch(note, searchText) switch
        {
            NoteSearchMatchKind.Title => 0,
            NoteSearchMatchKind.Body => 1,
            _ => 2
        };
    }

    private static int GetFavoriteRank(Note note)
    {
        return note.IsFavorite ? 0 : 1;
    }
}
