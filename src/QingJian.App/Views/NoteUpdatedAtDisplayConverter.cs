using System.Globalization;
using System.Windows.Data;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public sealed class NoteUpdatedAtDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTime updatedAt
            ? NoteNavigationDateHelper.FormatUpdatedAt(updatedAt, DateTime.Now)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
