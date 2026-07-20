using System.ComponentModel;
using System.Globalization;
using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public sealed class NoteNavigationGroupDescription : GroupDescription
{
    private readonly Func<DateTime> _localNow;
    private readonly Func<string> _searchText;
    private readonly Func<NoteNavigationSortMode> _sortMode;

    public NoteNavigationGroupDescription(Func<DateTime> localNow)
        : this(localNow, () => string.Empty, () => NoteNavigationSortMode.Time)
    {
    }

    public NoteNavigationGroupDescription(
        Func<DateTime> localNow,
        Func<string> searchText,
        Func<NoteNavigationSortMode> sortMode)
    {
        _localNow = localNow;
        _searchText = searchText;
        _sortMode = sortMode;
    }

    public override object GroupNameFromItem(object item, int level, CultureInfo culture)
    {
        return item is Note note
            ? NoteNavigationHelper.GetGroupName(note, _searchText(), _sortMode(), _localNow())
            : string.Empty;
    }
}
