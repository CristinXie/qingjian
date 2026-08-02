using System.Collections.ObjectModel;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class RecycleBinViewModel : ViewModelBase
{
    private readonly INoteService _noteService;

    public RecycleBinViewModel(INoteService noteService)
    {
        _noteService = noteService;
    }

    public ObservableCollection<Note> DeletedNotes { get; } = new();

    public bool IsEmpty => DeletedNotes.Count == 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var notes = await _noteService.GetRecentlyDeletedNotesAsync(cancellationToken);
        DeletedNotes.Clear();
        foreach (var note in notes)
        {
            DeletedNotes.Add(note);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task RestoreAsync(Note note, CancellationToken cancellationToken = default)
    {
        await _noteService.RestoreNoteAsync(note, cancellationToken);
        DeletedNotes.Remove(note);
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task PermanentlyDeleteAsync(
        Note note,
        CancellationToken cancellationToken = default)
    {
        await _noteService.PermanentlyDeleteNoteAsync(note, cancellationToken);
        DeletedNotes.Remove(note);
        OnPropertyChanged(nameof(IsEmpty));
    }
}
