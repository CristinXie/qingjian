using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly TimeSpan _autoSaveDelay;
    private CancellationTokenSource? _autoSaveCancellation;
    private Note? _selectedNote;
    private bool _isBusy;
    private bool _isLoadingSelection;

    public MainViewModel(INoteService noteService)
        : this(noteService, TimeSpan.FromMilliseconds(700), () => DateTime.Now)
    {
    }

    public MainViewModel(INoteService noteService, TimeSpan autoSaveDelay)
        : this(noteService, autoSaveDelay, () => DateTime.Now)
    {
    }

    public MainViewModel(INoteService noteService, TimeSpan autoSaveDelay, Func<DateTime> localNow)
    {
        _noteService = noteService;
        _autoSaveDelay = autoSaveDelay;
        NotesView = new ListCollectionView(Notes);
        NotesView.SortDescriptions.Add(new SortDescription(nameof(Note.UpdatedAt), ListSortDirection.Descending));
        NotesView.GroupDescriptions.Add(new NoteNavigationGroupDescription(localNow));
        NewNoteCommand = new AsyncRelayCommand(NewNoteAsync);
        DeleteSelectedNoteCommand = new AsyncRelayCommand(DeleteSelectedNoteAsync, () => SelectedNote is not null);
    }

    public ObservableCollection<Note> Notes { get; } = new();

    public ListCollectionView NotesView { get; }

    public Note? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote == value)
            {
                return;
            }

            if (_selectedNote is not null)
            {
                _selectedNote.PropertyChanged -= OnSelectedNotePropertyChanged;
            }

            _selectedNote = value;

            if (_selectedNote is not null)
            {
                _selectedNote.PropertyChanged += OnSelectedNotePropertyChanged;
            }

            OnPropertyChanged();
            DeleteSelectedNoteCommand.RaiseCanExecuteChanged();
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
        _isLoadingSelection = true;

        try
        {
            await _noteService.InitializeAsync(cancellationToken);
            var notes = await _noteService.GetActiveNotesAsync(cancellationToken);

            Notes.Clear();
            foreach (var note in notes)
            {
                Notes.Add(note);
            }

            RefreshNoteNavigation();
            SelectedNote = Notes.FirstOrDefault();
            OnPropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            _isLoadingSelection = false;
            IsBusy = false;
        }
    }

    public async Task NewNoteAsync()
    {
        var note = await _noteService.CreateNoteAsync();
        Notes.Insert(0, note);
        RefreshNoteNavigation();
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
        RefreshNoteNavigation();

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

    public void AddSavedNote(Note note, bool select)
    {
        var wasEmpty = IsEmpty;

        Notes.Insert(0, note);
        RefreshNoteNavigation();

        if (select)
        {
            SelectedNote = note;
        }

        if (wasEmpty)
        {
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public Task SaveSelectedNoteNowAsync(CancellationToken cancellationToken = default)
    {
        _autoSaveCancellation?.Cancel();

        if (SelectedNote is null)
        {
            return Task.CompletedTask;
        }

        return SaveSelectedNoteAsync(cancellationToken);
    }

    public void RefreshNoteNavigation()
    {
        NotesView.Refresh();
    }

    private void OnSelectedNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingSelection || e.PropertyName is not (nameof(Note.Title) or nameof(Note.Content)))
        {
            return;
        }

        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        _autoSaveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _autoSaveCancellation = cancellation;

        _ = SaveAfterDelayAsync(cancellation.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_autoSaveDelay, cancellationToken);
            await SaveSelectedNoteAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SaveSelectedNoteAsync(CancellationToken cancellationToken)
    {
        if (SelectedNote is null)
        {
            return;
        }

        await _noteService.SaveNoteAsync(SelectedNote, cancellationToken);
        MoveSelectedNoteToTop();
        RefreshNoteNavigation();
    }

    private void MoveSelectedNoteToTop()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var index = Notes.IndexOf(SelectedNote);
        if (index > 0)
        {
            Notes.Move(index, 0);
        }
    }
}
