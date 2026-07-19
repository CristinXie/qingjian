using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QingJian.App.Models;

public sealed class TodoItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private bool _isCompleted;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateOnly Date { get; set; }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsDeleted { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
