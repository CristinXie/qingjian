using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isUpdatingTitlePlaceholder;

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
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        UpdateTitlePlaceholderState();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await _viewModel.SaveSelectedNoteNowAsync();
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
