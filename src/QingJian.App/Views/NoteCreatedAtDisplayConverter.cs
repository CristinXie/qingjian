using System.Globalization;
using System.Windows.Data;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public sealed class NoteCreatedAtDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTime createdAt
            ? NoteNavigationDateHelper.FormatCreatedAt(createdAt, DateTime.Now)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
