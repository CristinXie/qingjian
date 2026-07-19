using System.Globalization;
using System.Windows.Data;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public sealed class TodoPreviewDisplayConverter : IValueConverter
{
    private readonly TodoTimeDisplayConverter _timeConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TodoItem todo)
        {
            return string.Empty;
        }

        var time = (string)_timeConverter.Convert(todo, typeof(string), parameter, culture);
        return string.IsNullOrEmpty(time) ? todo.Text : $"{time} {todo.Text}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
