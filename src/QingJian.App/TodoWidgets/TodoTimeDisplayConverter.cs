using System.Globalization;
using System.Windows.Data;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public sealed class TodoTimeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoItem todo || todo.StartTime is null)
        {
            return string.Empty;
        }

        var start = todo.StartTime.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (todo.EndTime is null)
        {
            return start;
        }

        return $"{start}-{todo.EndTime.Value.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
