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
    private readonly IFolderService? _folderService;
    private readonly TimeSpan _autoSaveDelay;
    private readonly object _notesSynchronization = new();
    private CancellationTokenSource? _autoSaveCancellation;
    private Note? _selectedNote;
    private bool _isBusy;
    private bool _isLoadingSelection;
    private string _searchText = string.Empty;
    private string? _currentFolderName;
    private NoteNavigationSortMode _navigationSortMode = NoteNavigationSortMode.Time;

    public MainViewModel(INoteService noteService)
        : this(noteService, null, TimeSpan.FromMilliseconds(700), () => DateTime.Now)
    {
    }

    public MainViewModel(INoteService noteService, TimeSpan autoSaveDelay)
        : this(noteService, null, autoSaveDelay, () => DateTime.Now)
    {
    }

    public MainViewModel(INoteService noteService, TimeSpan autoSaveDelay, Func<DateTime> localNow)
        : this(noteService, null, autoSaveDelay, localNow)
    {
    }

    public MainViewModel(INoteService noteService, IFolderService folderService)
        : this(noteService, folderService, TimeSpan.FromMilliseconds(700), () => DateTime.Now)
    {
    }

    public MainViewModel(
        INoteService noteService,
        IFolderService? folderService,
        TimeSpan autoSaveDelay,
        Func<DateTime> localNow)
    {
        _noteService = noteService;
        _folderService = folderService;
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

    public ObservableCollection<FolderSummary> Folders { get; } = new();

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

    public string? CurrentFolderName
    {
        get => _currentFolderName;
        private set => SetField(ref _currentFolderName, value);
    }

    public bool HasFolderFilter => CurrentFolderName is not null;

    public string CurrentFolderDisplayName => CurrentFolderName ?? FolderNamePolicy.AllNotesName;

    public bool ShowFolderEmptyState => HasFolderFilter
        && Folders.FirstOrDefault(folder =>
            string.Equals(folder.Name, CurrentFolderName, StringComparison.OrdinalIgnoreCase))?.NoteCount == 0;

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
            if (_folderService is not null)
            {
                await _folderService.InitializeAsync(cancellationToken);
                await RefreshFoldersAsync(cancellationToken);
            }

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
        var note = CurrentFolderName is null
            ? await _noteService.CreateNoteAsync()
            : await _noteService.CreateNoteInFolderAsync(CurrentFolderName);
        lock (_notesSynchronization)
        {
            Notes.Insert(0, note);
        }

        RefreshNoteNavigation();
        SelectedNote = note;
        if (_folderService is not null)
        {
            await RefreshFoldersAsync();
        }
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

    public void ApplyFolderFilter(string? folderName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(folderName)
            ? null
            : FolderNamePolicy.NormalizeDisplayName(folderName);
        CurrentFolderName = normalizedName;
        RefreshNoteNavigation();

        if (SelectedNote is not null && NotesView.Cast<Note>().Contains(SelectedNote))
        {
            return;
        }

        SelectedNote = NotesView.Cast<Note>().FirstOrDefault();
    }

    public async Task RefreshFoldersAsync(CancellationToken cancellationToken = default)
    {
        if (_folderService is null)
        {
            return;
        }

        var folders = await _folderService.GetFoldersAsync(cancellationToken);
        lock (_notesSynchronization)
        {
            Folders.Clear();
            foreach (var folder in folders)
            {
                Folders.Add(folder);
            }
        }

        if (CurrentFolderName is not null
            && !Folders.Any(folder =>
                string.Equals(folder.Name, CurrentFolderName, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentFolderName = FolderNamePolicy.UncategorizedName;
        }

        RefreshNoteNavigation();
    }

    public void ApplyFolderRename(string oldName, string newName)
    {
        if (string.Equals(CurrentFolderName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            CurrentFolderName = newName;
        }

        foreach (var note in Notes.Where(note =>
                     string.Equals(note.FolderName, oldName, StringComparison.OrdinalIgnoreCase)))
        {
            note.FolderName = newName;
        }

        RefreshNoteNavigation();
    }

    public void ApplyFolderDeletion(string deletedName)
    {
        if (string.Equals(CurrentFolderName, deletedName, StringComparison.OrdinalIgnoreCase))
        {
            CurrentFolderName = FolderNamePolicy.UncategorizedName;
        }

        foreach (var note in Notes.Where(note =>
                     string.Equals(note.FolderName, deletedName, StringComparison.OrdinalIgnoreCase)))
        {
            note.FolderName = FolderNamePolicy.UncategorizedName;
        }

        RefreshNoteNavigation();
    }

    public async Task MoveNoteAsync(
        Note note,
        string folderName,
        CancellationToken cancellationToken = default)
    {
        var visibleNotes = NotesView.Cast<Note>().ToList();
        var originalIndex = visibleNotes.IndexOf(note);
        var wasSelected = ReferenceEquals(SelectedNote, note);

        await _noteService.MoveNoteAsync(note, folderName, cancellationToken);
        if (_folderService is not null)
        {
            await RefreshFoldersAsync(cancellationToken);
        }
        RefreshNoteNavigation();

        if (!wasSelected || NotesView.Cast<Note>().Contains(note))
        {
            return;
        }

        var remainingNotes = NotesView.Cast<Note>().ToList();
        SelectedNote = remainingNotes.Count == 0
            ? null
            : remainingNotes[Math.Min(Math.Max(originalIndex, 0), remainingNotes.Count - 1)];
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
        if (item is not Note note)
        {
            return false;
        }

        if (CurrentFolderName is not null
            && !string.Equals(note.FolderName, CurrentFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SearchText)
            || NoteNavigationHelper.GetSearchMatch(note, SearchText) != NoteSearchMatchKind.None;
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
        OnPropertyChanged(nameof(ShowFolderEmptyState));
        OnPropertyChanged(nameof(HasFolderFilter));
        OnPropertyChanged(nameof(CurrentFolderDisplayName));
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
