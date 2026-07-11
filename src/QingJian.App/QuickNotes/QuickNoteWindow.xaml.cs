using System.Windows;
using System.Windows.Input;

namespace QingJian.App.QuickNotes;

public partial class QuickNoteWindow : Window, IQuickNoteWindow
{
    private bool _isSaved;

    public QuickNoteWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateSaveButtonState();
            BodyTextBox.Focus();
        };
    }

    public event EventHandler<QuickNoteDraft>? SaveRequested;

    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        _isSaved = true;
        Close();
    }

    public void ShowSaveError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void DragHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        RequestSave();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        RequestCancel();
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            RequestSave();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            RequestCancel();
            e.Handled = true;
        }
    }

    private void BodyTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSaveButtonState();
        ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void RequestSave()
    {
        if (!QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            ShowSaveError("请输入便签内容。");
            return;
        }

        SaveRequested?.Invoke(this, new QuickNoteDraft(TitleTextBox.Text, BodyTextBox.Text));
    }

    private void RequestCancel()
    {
        if (_isSaved || !QuickNoteTitleGenerator.HasBody(BodyTextBox.Text))
        {
            Close();
            return;
        }

        var result = MessageBox.Show(
            this,
            "要丢弃这条未保存的便签吗？",
            "QingJian",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private void UpdateSaveButtonState()
    {
        SaveButton.IsEnabled = QuickNoteTitleGenerator.HasBody(BodyTextBox.Text);
    }
}

public sealed class QuickNoteWindowFactory : IQuickNoteWindowFactory
{
    public IQuickNoteWindow Create()
    {
        return new QuickNoteWindow();
    }
}
