using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using QingJian.App.Editor;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppSettingsService _settingsService;
    private readonly AttachmentService _attachmentService;
    private readonly TodoWidgetCoordinator _todoWidgetCoordinator;
    private readonly MarkdownEditorState _editorState = new();
    private bool _isEditorReady;
    private bool _isUpdatingTitlePlaceholder;
    private bool _isCloseSaveInProgress;
    private bool _isClosingAfterSave;
    private int _editorLoadVersion;
    private readonly SemaphoreSlim _editorLoadSemaphore = new(1, 1);
    private int _pendingEditorLoadVersion;
    private Uri? _editorUri;
    private string _currentEditorMode = AppSettings.DefaultEditorMode;
    private string? _pendingEditorNoteId;
    private string? _pendingEditorMarkdown;

    public MainWindow(
        MainViewModel viewModel,
        AppSettingsService settingsService,
        AttachmentService attachmentService,
        TodoWidgetCoordinator todoWidgetCoordinator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        _attachmentService = attachmentService;
        _todoWidgetCoordinator = todoWidgetCoordinator;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.SelectedNote))
            {
                UpdateTitlePlaceholderState();
                _ = LoadSelectedNoteIntoEditorAsync();
            }
        };
    }

    private void ToggleTodoWidgetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _todoWidgetCoordinator.ToggleWidgetVisibility();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _currentEditorMode = (await _settingsService.LoadAsync()).EditorMode;
        await InitializeMarkdownEditorAsync();
        await _viewModel.LoadAsync();
        UpdateTitlePlaceholderState();
        await LoadSelectedNoteIntoEditorAsync();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosingAfterSave)
        {
            return;
        }

        e.Cancel = true;

        if (_isCloseSaveInProgress)
        {
            return;
        }

        _isCloseSaveInProgress = true;

        try
        {
            await PullLatestEditorMarkdownAsync();
            await _viewModel.SaveSelectedNoteNowAsync();

            _isClosingAfterSave = true;
            Closing -= OnClosing;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Unable to save the selected note before closing.\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isCloseSaveInProgress = false;
        }
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
            await MarkdownWebView.EnsureCoreWebView2Async();
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
}
