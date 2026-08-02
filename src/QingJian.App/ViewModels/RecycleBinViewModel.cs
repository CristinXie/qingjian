using System.Collections.ObjectModel;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class RecycleBinViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private Note? _selectedNote;

    public RecycleBinViewModel(INoteService noteService)
    {
        _noteService = noteService;
    }

    public ObservableCollection<Note> DeletedNotes { get; } = new();

    public Note? SelectedNote
    {
        get => _selectedNote;
        set => SetField(ref _selectedNote, value);
    }

    public bool IsEmpty => DeletedNotes.Count == 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var notes = await _noteService.GetRecentlyDeletedNotesAsync(cancellationToken);
        DeletedNotes.Clear();
        foreach (var note in notes)
        {
            DeletedNotes.Add(note);
        }

        SelectedNote = DeletedNotes.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task RestoreAsync(Note note, CancellationToken cancellationToken = default)
    {
        await _noteService.RestoreNoteAsync(note, cancellationToken);
        RemoveNote(note);
    }

    public async Task PermanentlyDeleteAsync(
        Note note,
        CancellationToken cancellationToken = default)
    {
        await _noteService.PermanentlyDeleteNoteAsync(note, cancellationToken);
        RemoveNote(note);
    }

    private void RemoveNote(Note note)
    {
        var index = DeletedNotes.IndexOf(note);
        var wasSelected = ReferenceEquals(SelectedNote, note);
        if (index < 0)
        {
            return;
        }

        DeletedNotes.RemoveAt(index);
        if (wasSelected)
        {
            SelectedNote = DeletedNotes.Count == 0
                ? null
                : DeletedNotes[Math.Min(index, DeletedNotes.Count - 1)];
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
