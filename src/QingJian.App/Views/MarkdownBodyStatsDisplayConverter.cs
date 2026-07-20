using System.Globalization;
using System.Windows.Data;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public sealed class MarkdownBodyStatsDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return NoteNavigationHelper.FormatBodyStats(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
