using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using QingJian.App.Models;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly TimeSpan _autoSaveDelay;
    private readonly object _notesSynchronization = new();
    private CancellationTokenSource? _autoSaveCancellation;
    private Note? _selectedNote;
    private bool _isBusy;
    private bool _isLoadingSelection;
    private string _searchText = string.Empty;
    private NoteNavigationSortMode _navigationSortMode = NoteNavigationSortMode.Time;

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
        BindingOperations.EnableCollectionSynchronization(Notes, _notesSynchronization);
        NotesView = new ListCollectionView(Notes)
        {
            Filter = FilterNote,
            CustomSort = new NoteNavigationComparer(() => NavigationSortMode, () => SearchText)
        };
        NotesView.GroupDescriptions.Add(new NoteNavigationGroupDescription(
            localNow,
            () => SearchText,
            () => NavigationSortMode));
        NewNoteCommand = new AsyncRelayCommand(NewNoteAsync);
        DeleteSelectedNoteCommand = new AsyncRelayCommand(DeleteSelectedNoteAsync, () => SelectedNote is not null);
        ToggleNavigationSortCommand = new RelayCommand(ToggleNavigationSort);
    }

    public ObservableCollection<Note> Notes { get; } = new();

    public ListCollectionView NotesView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value ?? string.Empty))
            {
                return;
            }

            RefreshNoteNavigation();
        }
    }

    public NoteNavigationSortMode NavigationSortMode
    {
        get => _navigationSortMode;
        private set => SetField(ref _navigationSortMode, value);
    }

    public string SortToggleToolTip => NavigationSortMode == NoteNavigationSortMode.Time
        ? "按收藏"
        : "按时间";

    public bool ShowNoSearchResults => !IsEmpty
        && !string.IsNullOrWhiteSpace(SearchText)
        && NotesView.IsEmpty;

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

    public bool IsEmpty
    {
        get
        {
            lock (_notesSynchronization)
            {
                return Notes.Count == 0;
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public AsyncRelayCommand NewNoteCommand { get; }

    public AsyncRelayCommand DeleteSelectedNoteCommand { get; }

    public RelayCommand ToggleNavigationSortCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        _isLoadingSelection = true;

        try
        {
            await _noteService.InitializeAsync(cancellationToken);
            var notes = await _noteService.GetActiveNotesAsync(cancellationToken);

            lock (_notesSynchronization)
            {
                Notes.Clear();
                foreach (var note in notes)
                {
                    Notes.Add(note);
                }
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
        lock (_notesSynchronization)
        {
            Notes.Insert(0, note);
        }

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

        var note = SelectedNote;
        int index;

        lock (_notesSynchronization)
        {
            index = Notes.IndexOf(note);
        }

        await _noteService.DeleteNoteAsync(note);
        lock (_notesSynchronization)
        {
            Notes.Remove(note);
        }

        RefreshNoteNavigation();

        lock (_notesSynchronization)
        {
            if (Notes.Count == 0)
            {
                SelectedNote = null;
            }
            else
            {
                SelectedNote = Notes[Math.Min(index, Notes.Count - 1)];
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    public void AddSavedNote(Note note, bool select)
    {
        var wasEmpty = IsEmpty;

        lock (_notesSynchronization)
        {
            Notes.Insert(0, note);
        }

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
        if (NotesView.Dispatcher.CheckAccess())
        {
            RefreshNoteNavigationCore();
            return;
        }

        NotesView.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(RefreshNoteNavigationCore));
    }

    public async Task ToggleFavoriteAsync(Note note)
    {
        await _noteService.SetFavoriteAsync(note, !note.IsFavorite);
        RefreshNoteNavigation();
    }

    private void OnSelectedNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingSelection
            || sender is not Note note
            || e.PropertyName is not (nameof(Note.Title) or nameof(Note.Content)))
        {
            return;
        }

        ScheduleAutoSave(note);
    }

    private bool FilterNote(object item)
    {
        return item is Note note
            && (string.IsNullOrWhiteSpace(SearchText)
                || NoteNavigationHelper.GetSearchMatch(note, SearchText) != NoteSearchMatchKind.None);
    }

    private void ToggleNavigationSort()
    {
        NavigationSortMode = NavigationSortMode == NoteNavigationSortMode.Time
            ? NoteNavigationSortMode.Favorite
            : NoteNavigationSortMode.Time;
        RefreshNoteNavigation();
    }

    private void RefreshNoteNavigationCore()
    {
        NotesView.Refresh();
        OnPropertyChanged(nameof(ShowNoSearchResults));
        OnPropertyChanged(nameof(SortToggleToolTip));
        OnPropertyChanged(nameof(SelectedNote));
    }

    private void ScheduleAutoSave(Note note)
    {
        _autoSaveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _autoSaveCancellation = cancellation;

        _ = SaveAfterDelayAsync(note, cancellation.Token);
    }

    private async Task SaveAfterDelayAsync(Note note, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_autoSaveDelay, cancellationToken);
            await SaveNoteAsync(note, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Task SaveSelectedNoteAsync(CancellationToken cancellationToken)
    {
        if (SelectedNote is null)
        {
            return Task.CompletedTask;
        }

        return SaveNoteAsync(SelectedNote, cancellationToken);
    }

    private async Task SaveNoteAsync(Note note, CancellationToken cancellationToken)
    {
        await _noteService.SaveNoteAsync(note, cancellationToken);
        if (ReferenceEquals(SelectedNote, note))
        {
            MoveSelectedNoteToTop();
        }

        RefreshNoteNavigation();
    }

    private void MoveSelectedNoteToTop()
    {
        if (SelectedNote is null)
        {
            return;
        }

        lock (_notesSynchronization)
        {
            var index = Notes.IndexOf(SelectedNote);
            if (index > 0)
            {
                Notes.Move(index, 0);
            }
        }
    }
}
