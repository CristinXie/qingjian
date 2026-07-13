using System.Globalization;
using System.Windows.Data;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public sealed class TodoItemsForDateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not DateOnly date ||
            values[1] is not IEnumerable<TodoItem> todos)
        {
            return Array.Empty<TodoItem>();
        }

        return todos.Where(todo => todo.Date == date).ToList();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
