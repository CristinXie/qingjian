using System.Collections.ObjectModel;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private Note? _selectedNote;
    private bool _isBusy;

    public MainViewModel(INoteService noteService)
    {
        _noteService = noteService;
        NewNoteCommand = new AsyncRelayCommand(NewNoteAsync);
        DeleteSelectedNoteCommand = new AsyncRelayCommand(DeleteSelectedNoteAsync, () => SelectedNote is not null);
    }

    public ObservableCollection<Note> Notes { get; } = new();

    public Note? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (SetField(ref _selectedNote, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                DeleteSelectedNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsEmpty => Notes.Count == 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public AsyncRelayCommand NewNoteCommand { get; }

    public AsyncRelayCommand DeleteSelectedNoteCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            await _noteService.InitializeAsync(cancellationToken);
            var notes = await _noteService.GetActiveNotesAsync(cancellationToken);

            Notes.Clear();
            foreach (var note in notes)
            {
                Notes.Add(note);
            }

            SelectedNote = Notes.FirstOrDefault();
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NewNoteAsync()
    {
        var note = await _noteService.CreateNoteAsync();
        Notes.Insert(0, note);
        SelectedNote = note;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task DeleteSelectedNoteAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var index = Notes.IndexOf(SelectedNote);
        var note = SelectedNote;

        await _noteService.DeleteNoteAsync(note);
        Notes.Remove(note);

        if (Notes.Count == 0)
        {
            SelectedNote = null;
        }
        else
        {
            SelectedNote = Notes[Math.Min(index, Notes.Count - 1)];
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    public Task SaveSelectedNoteNowAsync(CancellationToken cancellationToken = default)
    {
        return SelectedNote is null
            ? Task.CompletedTask
            : _noteService.SaveNoteAsync(SelectedNote, cancellationToken);
    }
}
