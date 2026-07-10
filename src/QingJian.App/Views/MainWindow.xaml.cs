using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using QingJian.App.Editor;
using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MarkdownEditorState _editorState = new();
    private bool _isEditorReady;
    private bool _isUpdatingTitlePlaceholder;
    private bool _isCloseSaveInProgress;
    private bool _isClosingAfterSave;
    private int _editorLoadVersion;
    private readonly SemaphoreSlim _editorLoadSemaphore = new(1, 1);
    private int _pendingEditorLoadVersion;
    private string? _pendingEditorNoteId;
    private string? _pendingEditorMarkdown;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
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
            MarkdownWebView.CoreWebView2.NavigationCompleted += MarkdownWebView_OnNavigationCompleted;

            var editorPath = Path.Combine(AppContext.BaseDirectory, "EditorAssets", "index.html");
            MarkdownWebView.Source = new Uri(editorPath);
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
        if (_viewModel.SelectedNote is null)
        {
            return;
        }

        if (!EditorMessage.TryParse(e.WebMessageAsJson, out var message))
        {
            return;
        }

        if (!_editorState.TryApplyEditorMarkdown(message.NoteId, message.Markdown, out var markdown))
        {
            return;
        }

        _viewModel.SelectedNote.Content = markdown;
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
