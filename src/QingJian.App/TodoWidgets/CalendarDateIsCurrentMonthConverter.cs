using System.Globalization;
using System.Windows.Data;

namespace QingJian.App.TodoWidgets;

public sealed class CalendarDateIsCurrentMonthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not DateOnly date ||
            values[1] is not DateOnly displayedMonth)
        {
            return false;
        }

        return date.Year == displayedMonth.Year && date.Month == displayedMonth.Month;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
