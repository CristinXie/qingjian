using QingJian.App.Models;

namespace QingJian.App.ViewModels;

public sealed class FolderListItemViewModel
{
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

    public FolderSummary ToSummary()
    {
        return new FolderSummary(Id, Name, IsSystem, NoteCount, IsValidMoveTarget);
    }
}
