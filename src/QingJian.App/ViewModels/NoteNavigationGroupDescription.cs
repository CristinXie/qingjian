using System.ComponentModel;
using System.Globalization;
using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public sealed class NoteNavigationGroupDescription : GroupDescription
{
    private readonly Func<DateTime> _localNow;

    public NoteNavigationGroupDescription(Func<DateTime> localNow)
    {
        _localNow = localNow;
    }

    public override object GroupNameFromItem(object item, int level, CultureInfo culture)
    {
        return item is Note note
            ? NoteNavigationDateHelper.GetGroupName(note.UpdatedAt, _localNow())
            : string.Empty;
    }
}
