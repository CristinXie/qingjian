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
        await PullLatestEditorMarkdownAsync();
        await _viewModel.SaveSelectedNoteNowAsync();
    }

    private async Task PullLatestEditorMarkdownAsync()
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null || _viewModel.SelectedNote is null)
        {
            return;
        }

        var result = await MarkdownWebView.ExecuteScriptAsync("window.qingjianEditor.getMarkdown();");
        var markdown = JsonSerializer.Deserialize<string>(result) ?? string.Empty;

        if (_editorState.TryApplyEditorMarkdown(markdown, out var normalizedMarkdown))
        {
            _viewModel.SelectedNote.Content = normalizedMarkdown;
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
            await SetEditorMarkdownAsync(_pendingEditorMarkdown);
            _pendingEditorMarkdown = null;
        }
    }

    private async Task LoadSelectedNoteIntoEditorAsync()
    {
        var markdown = _editorState.BeginLoad(_viewModel.SelectedNote?.Id, _viewModel.SelectedNote?.Content);

        if (!_isEditorReady)
        {
            _pendingEditorMarkdown = markdown;
            return;
        }

        await SetEditorMarkdownAsync(markdown);
        _editorState.EndLoad();
    }

    private async Task SetEditorMarkdownAsync(string markdown)
    {
        if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
        {
            _pendingEditorMarkdown = markdown;
            return;
        }

        var json = JsonSerializer.Serialize(markdown);
        await MarkdownWebView.ExecuteScriptAsync($"window.qingjianEditor.setMarkdown({json});");
        _editorState.EndLoad();
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

        if (!_editorState.TryApplyEditorMarkdown(message.Markdown, out var markdown))
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
