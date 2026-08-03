using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using QingJian.App.Editor;
using QingJian.App.Models;
using QingJian.App.Settings;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using QingJian.App.Tray;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private readonly AttachmentService _attachmentService;
    private readonly TodoWidgetCoordinator _todoWidgetCoordinator;
    private readonly IFolderService _folderService;
    private readonly WindowBehaviorCoordinator _windowBehaviorCoordinator;
    private readonly string _webView2UserDataFolder;
    private readonly MarkdownEditorState _editorState = new();
    private bool _isEditorReady;
    private bool _isUpdatingTitlePlaceholder;
    private bool _isCloseSaveInProgress;
    private bool _isClosingAfterSave;
    private bool _isWindowTransitionInProgress;
    private bool _isExplicitExitRequested;
    private bool _trayAvailable;
    private WindowState _restoreWindowState = WindowState.Normal;
    private int _editorLoadVersion;
    private readonly SemaphoreSlim _editorLoadSemaphore = new(1, 1);
    private int _pendingEditorLoadVersion;
    private Uri? _editorUri;
    private string _currentEditorMode = AppSettings.DefaultEditorMode;
    private string? _pendingEditorNoteId;
    private string? _pendingEditorMarkdown;

    public event EventHandler? SettingsRequested;

    public event EventHandler? FolderManagementRequested;

    public event Func<Task>? RecycleBinRequested;

    public event EventHandler? ApplicationExitRequested;

    public MainWindow(
        MainViewModel viewModel,
        AppSettingsService settingsService,
        AttachmentService attachmentService,
        TodoWidgetCoordinator todoWidgetCoordinator,
        IFolderService folderService)
        : this(
            viewModel,
            settingsService,
            attachmentService,
            todoWidgetCoordinator,
            folderService,
            new WindowBehaviorCoordinator(WindowBehaviorPreferences.Default),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QingJian",
                "WebView2"))
    {
    }

    public MainWindow(
        MainViewModel viewModel,
        AppSettingsService settingsService,
        AttachmentService attachmentService,
        TodoWidgetCoordinator todoWidgetCoordinator,
        IFolderService folderService,
        WindowBehaviorCoordinator windowBehaviorCoordinator)
        : this(
            viewModel,
            settingsService,
            attachmentService,
            todoWidgetCoordinator,
            folderService,
            windowBehaviorCoordinator,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QingJian",
                "WebView2"))
    {
    }

    public MainWindow(
        MainViewModel viewModel,
        AppSettingsService settingsService,
        AttachmentService attachmentService,
        TodoWidgetCoordinator todoWidgetCoordinator,
        IFolderService folderService,
        WindowBehaviorCoordinator windowBehaviorCoordinator,
        string webView2UserDataFolder)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        _attachmentService = attachmentService;
        _todoWidgetCoordinator = todoWidgetCoordinator;
        _folderService = folderService;
        _windowBehaviorCoordinator = windowBehaviorCoordinator;
        _webView2UserDataFolder = webView2UserDataFolder;
        DataContext = _viewModel;
        _todoWidgetCoordinator.VisibilityChanged += OnTodoWidgetVisibilityChanged;
        UpdateTodoWidgetToggleLabel(_todoWidgetCoordinator.IsVisible);
        Loaded += OnLoaded;
        Activated += (_, _) => _viewModel.RefreshNoteNavigation();
        Closing += OnClosing;
        StateChanged += OnStateChanged;
        UpdateModeToggleToolTip();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.SelectedNote))
            {
                UpdateTitlePlaceholderState();
                SynchronizeNavigationSelection();
                _ = LoadSelectedNoteIfChangedAsync();
            }
        };
    }

    public void ApplyWindowBehaviorPreferences(WindowBehaviorPreferences preferences)
    {
        _windowBehaviorCoordinator.Apply(preferences);
    }

    public void SetTrayAvailable(bool isAvailable)
    {
        _trayAvailable = isAvailable;
    }

    public void ShowFromTray()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ShowFromTray);
            return;
        }

        if (_isClosingAfterSave || _isExplicitExitRequested)
        {
            return;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = _restoreWindowState;
        Activate();
    }

    public void StartHiddenInTray()
    {
        HideToTrayCore();
    }

    public void RequestApplicationExit()
    {
        _isExplicitExitRequested = true;
        Close();
    }

    private void SynchronizeNavigationSelection()
    {
        if (_viewModel.IsBatchMode)
        {
            return;
        }

        var selectedNote = _viewModel.SelectedNote;
        NoteListBox.SelectedItem = selectedNote;
        if (selectedNote is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (!_viewModel.IsBatchMode && ReferenceEquals(_viewModel.SelectedNote, selectedNote))
            {
                NoteListBox.SelectedItem = selectedNote;
                NoteListBox.ScrollIntoView(selectedNote);
            }
        });
    }

    private async void ToggleTodoWidgetButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _todoWidgetCoordinator.ToggleWidgetVisibilityAsync();
    }

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        FolderManagementRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void RecycleBinButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (RecycleBinRequested is null)
        {
            return;
        }

        try
        {
            await PullLatestEditorMarkdownAsync();
            await _viewModel.SaveSelectedNoteNowAsync();
            await RecycleBinRequested.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"打开回收站失败。\n\n{ex.Message}",
                "回收站",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void BatchManagementButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await PullLatestEditorMarkdownAsync();
            await _viewModel.SaveSelectedNoteNowAsync();
            _viewModel.EnterBatchMode();
            NoteListBox.UnselectAll();
            SynchronizeBatchSelection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"进入批量管理前无法保存当前便签。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ClearFolderFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ApplyFolderFilterAsync(null);
    }

    private void OnTodoWidgetVisibilityChanged(object? sender, bool isVisible)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateTodoWidgetToggleLabel(isVisible));
            return;
        }

        UpdateTodoWidgetToggleLabel(isVisible);
    }

    private void UpdateTodoWidgetToggleLabel(bool isVisible)
    {
        TodoWidgetToggleButton.Content = TodoWidgetVisibilityAction.GetLabel(isVisible);
    }

    private void NoteListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.IsBatchMode)
        {
            SynchronizeBatchSelection();
            return;
        }

        if (e.AddedItems.OfType<Note>().FirstOrDefault() is { } note)
        {
            _viewModel.SelectedNote = note;
        }
    }

    private void SynchronizeBatchSelection()
    {
        _viewModel.SetBatchSelection(NoteListBox.SelectedItems.Cast<Note>());
    }

    private void BatchSelectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        NoteListBox.SelectAll();
        SynchronizeBatchSelection();
    }

    private void BatchClearSelectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        NoteListBox.UnselectAll();
        SynchronizeBatchSelection();
    }

    private void BatchExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        ExitBatchMode();
    }

    private void ExitBatchMode()
    {
        NoteListBox.UnselectAll();
        _viewModel.ExitBatchMode();
        NoteListBox.SelectedItem = _viewModel.SelectedNote;
    }

    private async void FavoriteButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: Note note })
        {
            return;
        }

        try
        {
            await _viewModel.ToggleFavoriteAsync(note);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"收藏状态保存失败。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void FolderAssignmentButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { DataContext: Note note } button)
        {
            return;
        }

        var menu = CreateFolderMoveMenu(
            button,
            new[] { note },
            folderName => MoveNoteToFolderAsync(note, folderName),
            () => CreateFolderAndMoveNoteAsync(note));
        menu.IsOpen = true;
    }

    private void BatchMoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var selectedNotes = NoteListBox.SelectedItems.Cast<Note>().ToArray();
        if (selectedNotes.Length == 0)
        {
            return;
        }

        var menu = CreateFolderMoveMenu(
            button,
            selectedNotes,
            MoveBatchSelectionToFolderAsync,
            CreateFolderAndMoveBatchSelectionAsync);
        menu.IsOpen = true;
    }

    private ContextMenu CreateFolderMoveMenu(
        Button placementTarget,
        IReadOnlyCollection<Note> notes,
        Func<string, Task> moveAsync,
        Func<Task> createAndMoveAsync)
    {
        var menu = new ContextMenu { PlacementTarget = placementTarget };
        foreach (var folder in _viewModel.Folders)
        {
            var allInFolder = notes.All(note => string.Equals(
                note.FolderName,
                folder.Name,
                StringComparison.OrdinalIgnoreCase));
            var menuItem = new MenuItem
            {
                Header = folder.Name,
                IsCheckable = true,
                IsChecked = allInFolder,
                IsEnabled = folder.IsValidMoveTarget && !allInFolder,
                Tag = folder.Name
            };
            menuItem.Click += async (_, _) => await moveAsync(folder.Name);
            menu.Items.Add(menuItem);
        }

        menu.Items.Add(new Separator());
        var createItem = new MenuItem { Header = "新建文件夹…" };
        createItem.Click += async (_, _) => await createAndMoveAsync();
        menu.Items.Add(createItem);
        return menu;
    }

    public async Task ApplyFolderFilterAsync(string? folderName)
    {
        await PullLatestEditorMarkdownAsync();
        await _viewModel.SaveSelectedNoteNowAsync();
        _viewModel.ApplyFolderFilter(folderName);
    }

    private async Task CreateFolderAndMoveNoteAsync(Note note)
    {
        var dialog = new FolderNameDialog(_folderService)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.CreatedFolderName is null)
        {
            return;
        }

        await _viewModel.RefreshFoldersAsync();
        await MoveNoteToFolderAsync(note, dialog.CreatedFolderName);
    }

    private async Task CreateFolderAndMoveBatchSelectionAsync()
    {
        var dialog = new FolderNameDialog(_folderService)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.CreatedFolderName is null)
        {
            return;
        }

        await _viewModel.RefreshFoldersAsync();
        await MoveBatchSelectionToFolderAsync(dialog.CreatedFolderName);
    }

    private async Task MoveNoteToFolderAsync(Note note, string folderName)
    {
        try
        {
            if (ReferenceEquals(_viewModel.SelectedNote, note))
            {
                await PullLatestEditorMarkdownAsync();
                await _viewModel.SaveSelectedNoteNowAsync();
            }

            await _viewModel.MoveNoteAsync(note, folderName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"移动便签失败。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task MoveBatchSelectionToFolderAsync(string folderName)
    {
        try
        {
            await _viewModel.MoveBatchSelectionAsync(folderName);
            NoteListBox.UnselectAll();
            SynchronizeBatchSelection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"批量移动便签失败。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void BatchFavoriteButton_OnClick(object sender, RoutedEventArgs e)
    {
        await SetBatchFavoriteAsync(true);
    }

    private async void BatchUnfavoriteButton_OnClick(object sender, RoutedEventArgs e)
    {
        await SetBatchFavoriteAsync(false);
    }

    private async Task SetBatchFavoriteAsync(bool isFavorite)
    {
        try
        {
            await _viewModel.SetBatchFavoriteAsync(isFavorite);
            NoteListBox.UnselectAll();
            SynchronizeBatchSelection();
        }
        catch (Exception ex)
        {
            var action = isFavorite ? "批量收藏失败。" : "批量取消收藏失败。";
            MessageBox.Show(
                this,
                $"{action}\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void BatchDeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        await DeleteBatchSelectionWithConfirmationAsync();
    }

    private async Task DeleteBatchSelectionWithConfirmationAsync()
    {
        var count = NoteListBox.SelectedItems.Count;
        if (count == 0)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            BatchNoteDeletionConfirmation.BuildPrompt(count),
            "批量删除便签",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (!BatchNoteDeletionConfirmation.IsConfirmed(result))
        {
            return;
        }

        try
        {
            await _viewModel.DeleteBatchSelectionAsync();
            NoteListBox.UnselectAll();
            SynchronizeBatchSelection();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"批量删除便签失败。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsBatchMode)
        {
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            NoteListBox.SelectAll();
            SynchronizeBatchSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await DeleteBatchSelectionWithConfirmationAsync();
        }
        else if (e.Key == Key.Escape)
        {
            ExitBatchMode();
            e.Handled = true;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _currentEditorMode = (await _settingsService.LoadAsync()).EditorMode;
        UpdateModeToggleToolTip();
        await InitializeMarkdownEditorAsync();
        await _viewModel.LoadAsync();
        UpdateTitlePlaceholderState();
        await LoadSelectedNoteIfChangedAsync();
    }

    private async void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            _restoreWindowState = WindowState;
            return;
        }

        if (!_windowBehaviorCoordinator.ShouldHideOnMinimize(_trayAvailable))
        {
            return;
        }

        await HideToTrayAfterSaveAsync("最小化到托盘前无法保存当前便签");
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosingAfterSave)
        {
            return;
        }

        e.Cancel = true;

        if (_isWindowTransitionInProgress)
        {
            return;
        }

        _isWindowTransitionInProgress = true;
        var hideToTray = _windowBehaviorCoordinator.ShouldHideOnClose(
            _isExplicitExitRequested,
            _trayAvailable);
        Dispatcher.BeginInvoke(new Action(() => _ = CompleteCloseAfterSaveAsync(hideToTray)));
    }

    private async Task CompleteCloseAfterSaveAsync(bool hideToTray)
    {
        _isCloseSaveInProgress = true;

        try
        {
            await PullLatestEditorMarkdownAsync();
            await _viewModel.SaveSelectedNoteNowAsync();

            if (hideToTray)
            {
                HideToTrayCore();
                return;
            }

            _isClosingAfterSave = true;
            Closing -= OnClosing;
            Close();
            ApplicationExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _isExplicitExitRequested = false;
            if (!IsVisible && !_isClosingAfterSave)
            {
                ShowFromTray();
            }

            MessageBox.Show(
                this,
                $"关闭前无法保存当前便签。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isCloseSaveInProgress = false;
            _isWindowTransitionInProgress = false;
        }
    }

    private async Task HideToTrayAfterSaveAsync(string errorTitle)
    {
        if (_isWindowTransitionInProgress)
        {
            return;
        }

        _isWindowTransitionInProgress = true;
        _isCloseSaveInProgress = true;
        try
        {
            await PullLatestEditorMarkdownAsync();
            await _viewModel.SaveSelectedNoteNowAsync();
            HideToTrayCore();
        }
        catch (Exception ex)
        {
            ShowFromTray();
            MessageBox.Show(
                this,
                $"{errorTitle}。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isCloseSaveInProgress = false;
            _isWindowTransitionInProgress = false;
        }
    }

    private void HideToTrayCore()
    {
        Hide();
        ShowInTaskbar = false;
        WindowState = _restoreWindowState;
    }

    private async Task PullLatestEditorMarkdownAsync()
    {
        var selectedNote = _viewModel.SelectedNote;
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null || selectedNote is null)
        {
            return;
        }

        await _editorLoadSemaphore.WaitAsync();
        try
        {
            var result = await MarkdownWebView.ExecuteScriptAsync("window.qingjianEditor.getMarkdown();");

            if (_viewModel.SelectedNote != selectedNote)
            {
                return;
            }

            var markdown = JsonSerializer.Deserialize<string>(result) ?? string.Empty;

            if (_editorState.TryApplyEditorMarkdown(selectedNote.Id, markdown, out var normalizedMarkdown))
            {
                selectedNote.Content = normalizedMarkdown;
            }
        }
        catch (Exception) when (_isClosingAfterSave || _isCloseSaveInProgress)
        {
            return;
        }
        finally
        {
            _editorLoadSemaphore.Release();
        }
    }

    private async Task InitializeMarkdownEditorAsync()
    {
        try
        {
            MarkdownWebView.WebMessageReceived += MarkdownWebView_OnWebMessageReceived;
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: _webView2UserDataFolder);
            await MarkdownWebView.EnsureCoreWebView2Async(environment);
            MarkdownWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                AttachmentService.AssetHostName,
                _attachmentService.AttachmentFolder,
                CoreWebView2HostResourceAccessKind.Allow);
            MarkdownWebView.CoreWebView2.NavigationStarting += MarkdownWebView_OnNavigationStarting;
            MarkdownWebView.CoreWebView2.NavigationCompleted += MarkdownWebView_OnNavigationCompleted;

            var editorPath = Path.Combine(AppContext.BaseDirectory, "EditorAssets", "index.html");
            _editorUri = new Uri(editorPath);
            MarkdownWebView.Source = _editorUri;
        }
        catch (Exception)
        {
            MarkdownEditorFallback.Visibility = Visibility.Visible;
            MarkdownWebView.Visibility = Visibility.Collapsed;
        }
    }

    private async void MarkdownWebView_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            MarkdownEditorFallback.Visibility = Visibility.Visible;
            MarkdownWebView.Visibility = Visibility.Collapsed;
            return;
        }

        _isEditorReady = true;
        await SetEditorModeAsync(_currentEditorMode);

        if (_pendingEditorMarkdown is not null)
        {
            var noteId = _pendingEditorNoteId;
            var markdown = _pendingEditorMarkdown;
            var loadVersion = _pendingEditorLoadVersion;
            await SetEditorMarkdownAsync(noteId, markdown, loadVersion);
            _pendingEditorMarkdown = null;
            _pendingEditorNoteId = null;
        }
    }

    private Task LoadSelectedNoteIfChangedAsync()
    {
        return _editorState.ShouldReloadSelection(_viewModel.SelectedNote?.Id)
            ? LoadSelectedNoteIntoEditorAsync()
            : Task.CompletedTask;
    }

    private async Task LoadSelectedNoteIntoEditorAsync()
    {
        var selectedNote = _viewModel.SelectedNote;
        var noteId = selectedNote?.Id;
        var markdown = _editorState.BeginLoad(noteId, selectedNote?.Content);
        var loadVersion = Interlocked.Increment(ref _editorLoadVersion);

        if (!_isEditorReady)
        {
            _pendingEditorNoteId = noteId;
            _pendingEditorMarkdown = markdown;
            _pendingEditorLoadVersion = loadVersion;
            return;
        }

        await SetEditorMarkdownAsync(noteId, markdown, loadVersion);
    }

    private async Task SetEditorMarkdownAsync(string? noteId, string markdown, int loadVersion)
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
        {
            _pendingEditorNoteId = noteId;
            _pendingEditorMarkdown = markdown;
            _pendingEditorLoadVersion = loadVersion;
            return;
        }

        await _editorLoadSemaphore.WaitAsync();
        try
        {
            if (loadVersion != _editorLoadVersion)
            {
                return;
            }

            var noteIdJson = JsonSerializer.Serialize(noteId ?? string.Empty);
            var markdownJson = JsonSerializer.Serialize(markdown);
            await MarkdownWebView.ExecuteScriptAsync($"window.qingjianEditor.setMarkdown({noteIdJson}, {markdownJson});");

            if (loadVersion == _editorLoadVersion)
            {
                _editorState.EndLoad();
            }
        }
        finally
        {
            _editorLoadSemaphore.Release();
        }
    }

    private void MarkdownWebView_OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!EditorMessage.TryParse(e.WebMessageAsJson, out var message))
        {
            return;
        }

        if (message.Type == EditorMessage.ExternalLinkRequestedType)
        {
            OpenExternalLink(message.Url);
            return;
        }

        if (message.Type == EditorMessage.EditorModeChangedType)
        {
            _currentEditorMode = message.EditorMode;
            UpdateModeToggleToolTip();
            _ = SaveEditorModeAsync(message.EditorMode);
            return;
        }

        if (message.Type == EditorMessage.LocalImageRequestedType)
        {
            _ = SaveLocalImageAsync(message);
            return;
        }

        if (message.Type == EditorMessage.NativePasteRequestedType)
        {
            _ = TryPasteImageFromClipboardAsync();
            return;
        }

        if (_viewModel.SelectedNote is null)
        {
            return;
        }

        if (!_editorState.TryApplyEditorMarkdown(message.NoteId, message.Markdown, out var markdown))
        {
            return;
        }

        _viewModel.SelectedNote.Content = markdown;
    }

    private void MarkdownWebView_OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_editorUri is not null &&
            Uri.TryCreate(e.Uri, UriKind.Absolute, out var targetUri) &&
            IsSameEditorPage(targetUri))
        {
            return;
        }

        e.Cancel = true;
    }

    private bool IsSameEditorPage(Uri targetUri)
    {
        return _editorUri is not null &&
            string.Equals(targetUri.LocalPath, _editorUri.LocalPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void OpenExternalLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
    }

    private async Task SetEditorModeAsync(string editorMode)
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
        {
            return;
        }

        var editorModeJson = JsonSerializer.Serialize(editorMode);
        await MarkdownWebView.ExecuteScriptAsync($"window.qingjianEditor.setEditorMode({editorModeJson});");
    }

    private async Task SaveEditorModeAsync(string editorMode)
    {
        var settings = await _settingsService.SaveEditorModeAsync(editorMode);
        _currentEditorMode = settings.EditorMode;
        UpdateModeToggleToolTip();
    }

    public async Task ApplyEditorModePreferenceAsync(string editorMode)
    {
        _currentEditorMode = new AppSettings(editorMode).Normalize().EditorMode;
        await SetEditorModeAsync(_currentEditorMode);
        UpdateModeToggleToolTip();
    }

    private async Task ExecuteEditorCommandAsync(string script)
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await MarkdownWebView.ExecuteScriptAsync(script);
        }
        catch (Exception)
        {
        }
    }

    private async void ModeToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteEditorCommandAsync("window.qingjianEditor.toggleMode();");
    }

    private async void UndoButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteEditorCommandAsync("window.qingjianEditor.undo();");
    }

    private async void RedoButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteEditorCommandAsync("window.qingjianEditor.redo();");
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedNote = _viewModel.SelectedNote;
        if (selectedNote is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            NoteDeletionConfirmation.BuildPrompt(selectedNote.Title),
            "删除便签",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (!NoteDeletionConfirmation.IsConfirmed(result) ||
            !ReferenceEquals(_viewModel.SelectedNote, selectedNote) ||
            !_viewModel.DeleteSelectedNoteCommand.CanExecute(null))
        {
            return;
        }

        _viewModel.DeleteSelectedNoteCommand.Execute(null);
    }

    private async Task SaveLocalImageAsync(EditorMessage message)
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var attachment = await _attachmentService.SaveImageAsync(message.FileName, message.DataUrl);
            var requestIdJson = JsonSerializer.Serialize(message.RequestId);
            var assetUrlJson = JsonSerializer.Serialize(attachment.AssetUrl);
            await MarkdownWebView.ExecuteScriptAsync(
                $"window.qingjianEditor.completeImageUpload({requestIdJson}, {assetUrlJson});");
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task<bool> TryPasteImageFromClipboardAsync()
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null || _viewModel.SelectedNote is null)
        {
            return false;
        }

        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var imagePaths = Clipboard.GetFileDropList()
                    .Cast<string>()
                    .Where(IsSupportedImagePath)
                    .ToArray();

                if (imagePaths.Length > 0)
                {
                    await InsertImageFilesAsync(imagePaths);
                    return true;
                }
            }

            if (Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                if (image is null)
                {
                    return false;
                }

                var attachment = await _attachmentService.SaveImageBytesAsync("clipboard.png", EncodePng(image));
                await InsertImageAssetAsync(attachment.AssetUrl);
                return true;
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (ExternalException)
        {
        }

        return false;
    }

    private async Task InsertImageFilesAsync(IEnumerable<string> imagePaths)
    {
        foreach (var imagePath in imagePaths)
        {
            try
            {
                var attachment = await _attachmentService.SaveImageFileAsync(imagePath);
                await InsertImageAssetAsync(attachment.AssetUrl);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private async Task InsertImageAssetAsync(string assetUrl)
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
        {
            return;
        }

        var assetUrlJson = JsonSerializer.Serialize(assetUrl);
        await MarkdownWebView.ExecuteScriptAsync($"window.qingjianEditor.insertImage({assetUrlJson});");
    }

    private static bool IsSupportedImagePath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
    }

    private static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void TitleTextBox_OnGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_viewModel.SelectedNote?.Title != NoteService.DefaultTitle)
        {
            UpdateTitlePlaceholderState();
            return;
        }

        _isUpdatingTitlePlaceholder = true;
        TitleTextBox.Text = string.Empty;
        TitleTextBox.Foreground = GetBrush("TextBrush", Brushes.Black);
        _isUpdatingTitlePlaceholder = false;
    }

    private async void TitleTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        UpdateTitleSource();
        e.Handled = true;
        MarkdownWebView.Focus();
        await ExecuteEditorCommandAsync("window.qingjianEditor.focus();");
    }

    private async void TitleTextBox_OnLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            _isUpdatingTitlePlaceholder = true;
            TitleTextBox.Text = NoteService.DefaultTitle;
            _isUpdatingTitlePlaceholder = false;
        }

        UpdateTitleSource();
        UpdateTitlePlaceholderState();
        await _viewModel.SaveSelectedNoteNowAsync();
    }

    private void TitleTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingTitlePlaceholder || !TitleTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            return;
        }

        UpdateTitleSource();
        TitleTextBox.Foreground = GetBrush("TextBrush", Brushes.Black);
    }

    private void UpdateTitleSource()
    {
        BindingOperations.GetBindingExpression(TitleTextBox, TextBox.TextProperty)?.UpdateSource();
    }

    private void UpdateTitlePlaceholderState()
    {
        if (TitleTextBox.IsKeyboardFocusWithin)
        {
            TitleTextBox.Foreground = GetBrush("TextBrush", Brushes.Black);
            return;
        }

        TitleTextBox.Foreground = _viewModel.SelectedNote?.Title == NoteService.DefaultTitle
            ? GetBrush("MutedTextBrush", Brushes.Gray)
            : GetBrush("TextBrush", Brushes.Black);
    }

    private Brush GetBrush(string key, Brush fallback)
    {
        return TryFindResource(key) as Brush ?? fallback;
    }

    private void UpdateModeToggleToolTip()
    {
        ModeToggleButton.ToolTip = _currentEditorMode == "markdown"
            ? "切换到所见即所得"
            : "切换到 Markdown";
    }
}
