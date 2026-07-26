using System.Collections.ObjectModel;
using QingJian.App.Services;

namespace QingJian.App.ViewModels;

public sealed class FolderManagementViewModel : ViewModelBase
{
    private readonly IFolderService _folderService;
    private FolderListItemViewModel? _editingItem;
    private bool _isEditing;
    private string _editorText = string.Empty;
    private string _errorMessage = string.Empty;
    private int _allNotesCount;

    public FolderManagementViewModel(IFolderService folderService)
    {
        _folderService = folderService;
        BeginCreateCommand = new RelayCommand(BeginCreate, () => !IsEditing);
        ConfirmEditCommand = new AsyncRelayCommand(ConfirmEditAsync, () => IsEditing);
        CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditing);
    }

    public ObservableCollection<FolderListItemViewModel> Items { get; } = new();

    public int AllNotesCount
    {
        get => _allNotesCount;
        private set => SetField(ref _allNotesCount, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (!SetField(ref _isEditing, value))
            {
                return;
            }

            BeginCreateCommand.RaiseCanExecuteChanged();
            ConfirmEditCommand.RaiseCanExecuteChanged();
            CancelEditCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsCreating => IsEditing && _editingItem is null;

    public FolderListItemViewModel? EditingItem
    {
        get => _editingItem;
        private set
        {
            if (ReferenceEquals(_editingItem, value))
            {
                return;
            }

            _editingItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCreating));
        }
    }

    public string EditorText
    {
        get => _editorText;
        set => SetField(ref _editorText, value ?? string.Empty);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public RelayCommand BeginCreateCommand { get; }

    public AsyncRelayCommand ConfirmEditCommand { get; }

    public RelayCommand CancelEditCommand { get; }

    public event Action<string, string>? FolderRenamed;

    public event Action<string>? FolderDeleted;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _folderService.GetFoldersAsync(cancellationToken);
        Items.Clear();
        foreach (var folder in folders
                     .OrderBy(folder => folder.IsSystem ? 0 : 1)
                     .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Items.Add(new FolderListItemViewModel(folder));
        }

        AllNotesCount = Items.Sum(item => item.NoteCount);
    }

    public void BeginRename(FolderListItemViewModel item)
    {
        if (!item.CanManage || IsEditing)
        {
            return;
        }

        EditingItem = item;
        EditorText = item.Name;
        ErrorMessage = string.Empty;
        IsEditing = true;
        OnPropertyChanged(nameof(IsCreating));
    }

    public async Task ConfirmEditAsync()
    {
        if (!IsEditing)
        {
            return;
        }

        try
        {
            if (EditingItem is null)
            {
                await _folderService.CreateAsync(EditorText);
            }
            else
            {
                var oldName = EditingItem.Name;
                var renamed = await _folderService.RenameAsync(EditingItem.ToSummary(), EditorText);
                FolderRenamed?.Invoke(oldName, renamed.Name);
            }

            await LoadAsync();
            EndEdit();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task DeleteAsync(
        FolderListItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        if (!item.CanManage)
        {
            return;
        }

        try
        {
            await _folderService.DeleteAsync(item.ToSummary(), cancellationToken);
            FolderDeleted?.Invoke(item.Name);
            await LoadAsync(cancellationToken);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void BeginCreate()
    {
        EditingItem = null;
        EditorText = string.Empty;
        ErrorMessage = string.Empty;
        IsEditing = true;
        OnPropertyChanged(nameof(IsCreating));
    }

    private void CancelEdit()
    {
        EndEdit();
    }

    private void EndEdit()
    {
        IsEditing = false;
        EditingItem = null;
        EditorText = string.Empty;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(IsCreating));
    }
}
