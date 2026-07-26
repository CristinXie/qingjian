using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class FolderManagementWindow : Window
{
    private readonly FolderManagementViewModel _viewModel;

    public FolderManagementWindow(FolderManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public string? SelectedFolderName { get; private set; }

    public bool SelectedAllNotes { get; private set; }

    private async void FolderManagementWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        AllNotesCountTextBlock.Text = $"({_viewModel.AllNotesCount} 条)";
    }

    private void NewFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.BeginCreateCommand.Execute(null);
        NewFolderTextBox.Focus();
    }

    private void AllNotesButton_OnClick(object sender, RoutedEventArgs e)
    {
        SelectedAllNotes = true;
        SelectedFolderName = null;
        DialogResult = true;
        Close();
    }

    private void FolderRow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Button || sender is not FrameworkElement { DataContext: FolderListItemViewModel item } || item.IsEditing)
        {
            return;
        }

        SelectedFolderName = item.Name;
        SelectedAllNotes = false;
        DialogResult = true;
        Close();
    }

    private void RenameFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: FolderListItemViewModel item })
        {
            _viewModel.BeginRename(item);
        }
    }

    private async void DeleteFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { DataContext: FolderListItemViewModel item })
        {
            return;
        }

        var prompt = item.NoteCount > 0
            ? $"确定删除文件夹“{item.Name}”吗？\n\n其中 {item.NoteCount} 条便签将移至未分类。"
            : $"确定删除空文件夹“{item.Name}”吗？";
        var result = MessageBox.Show(
            this,
            prompt,
            "删除文件夹",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await _viewModel.DeleteAsync(item);
        AllNotesCountTextBlock.Text = $"({_viewModel.AllNotesCount} 条)";
    }

    private void FolderManagementWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        DialogResult = false;
        Close();
        e.Handled = true;
    }
}
