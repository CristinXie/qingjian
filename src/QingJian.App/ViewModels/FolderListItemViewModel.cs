using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public sealed class FolderListItemViewModel : ViewModelBase
{
    private bool _isEditing;
    private string _editText = string.Empty;

    public FolderListItemViewModel(FolderSummary summary)
    {
        Id = summary.Id;
        Name = summary.Name;
        NoteCount = summary.NoteCount;
        IsSystem = summary.IsSystem;
        IsValidMoveTarget = summary.IsValidMoveTarget;
    }

    public string Id { get; }

    public string Name { get; }

    public int NoteCount { get; }

    public bool IsSystem { get; }

    public bool IsValidMoveTarget { get; }

    public bool CanManage => !IsSystem;

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    public string EditText
    {
        get => _editText;
        set => SetField(ref _editText, value ?? string.Empty);
    }

    public FolderSummary ToSummary()
    {
        return new FolderSummary(Id, Name, IsSystem, NoteCount, IsValidMoveTarget);
    }
}
