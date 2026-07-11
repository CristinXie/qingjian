using QingJian.App.Models;

namespace QingJian.App.QuickNotes;

public sealed record QuickNoteSaveResult(bool Succeeded, Note? Note, string? ErrorMessage)
{
    public static QuickNoteSaveResult Success(Note note) => new(true, note, null);

    public static QuickNoteSaveResult Failure(string errorMessage) => new(false, null, errorMessage);
}
