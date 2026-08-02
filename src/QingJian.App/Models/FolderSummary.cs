namespace QingJian.App.Models;

public sealed record FolderSummary(
    string Id,
    string Name,
    bool IsSystem,
    int NoteCount,
    bool IsValidMoveTarget);
