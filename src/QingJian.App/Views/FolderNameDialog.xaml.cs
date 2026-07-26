using System.Windows;
using System.Windows.Input;
using QingJian.App.Services;

namespace QingJian.App.Views;

public partial class FolderNameDialog : Window
{
    private readonly IFolderService _folderService;
    private bool _isSubmitting;

    public FolderNameDialog(IFolderService folderService)
    {
        InitializeComponent();
        _folderService = folderService;
    }

    public string? CreatedFolderName { get; private set; }

    private void FolderNameDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        FolderNameTextBox.Focus();
    }

    private async void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        await SubmitAsync();
    }

    public async Task SubmitAsync()
    {
        if (_isSubmitting)
        {
            return;
        }

        _isSubmitting = true;
        ConfirmButton.IsEnabled = false;
        ErrorTextBlock.Text = string.Empty;
        try
        {
            var folder = await _folderService.CreateAsync(FolderNameTextBox.Text);
            CreatedFolderName = folder.Name;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
            ConfirmButton.IsEnabled = true;
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void FolderNameDialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SubmitAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }
}
