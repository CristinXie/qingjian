using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QingJian.App.Models;

public sealed class Note : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private bool _isFavorite;
    private DateTime? _favoritedAt;
    private string _folderName = "未分类";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string Content
    {
        get => _content;
        set => SetField(ref _content, value);
    }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }

    public DateTime? FavoritedAt
    {
        get => _favoritedAt;
        set => SetField(ref _favoritedAt, value);
    }

    public string FolderName
    {
        get => _folderName;
        set => SetField(ref _folderName, string.IsNullOrWhiteSpace(value) ? "未分类" : value);
    }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

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
