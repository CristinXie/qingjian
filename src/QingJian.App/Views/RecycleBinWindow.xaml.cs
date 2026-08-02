using System.Windows;
using System.Windows.Controls;
using QingJian.App.Models;
using QingJian.App.ViewModels;

namespace QingJian.App.Views;

public partial class RecycleBinWindow : Window
{
    private readonly RecycleBinViewModel _viewModel;

    public RecycleBinWindow(RecycleBinViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"加载回收站失败。\n\n{ex.Message}",
                "回收站",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RestoreNoteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Note note })
        {
            return;
        }

        try
        {
            await _viewModel.RestoreAsync(note);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"恢复便签失败。\n\n{ex.Message}",
                "回收站",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void PermanentlyDeleteNoteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: Note note })
        {
            return;
        }

        try
        {
            await _viewModel.PermanentlyDeleteAsync(note);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"永久删除便签失败。\n\n{ex.Message}",
                "回收站",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
