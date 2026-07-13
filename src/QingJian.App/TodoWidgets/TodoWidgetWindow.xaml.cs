using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QingJian.App.Models;

namespace QingJian.App.TodoWidgets;

public partial class TodoWidgetWindow : Window
{
    private readonly TodoWidgetViewModel _viewModel;
    private readonly DesktopLayerService _desktopLayerService;
    private readonly TodoWidgetCoordinator _coordinator;
    private bool _isHidingFromButton;

    public TodoWidgetWindow(
        TodoWidgetViewModel viewModel,
        DesktopLayerService desktopLayerService,
        TodoWidgetCoordinator coordinator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _desktopLayerService = desktopLayerService;
        _coordinator = coordinator;
        DataContext = _viewModel;
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (!_desktopLayerService.TryAttachToDesktop(this))
        {
            Topmost = false;
        }
    }

    private void Window_OnMouseEnter(object sender, MouseEventArgs e)
    {
        RootBorder.Opacity = Math.Min(0.95, _viewModel.Opacity + 0.15);
    }

    private void Window_OnMouseLeave(object sender, MouseEventArgs e)
    {
        RootBorder.Opacity = _viewModel.Opacity;
    }

    private void DragHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsLocked || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void EightDayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.EightDay);
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void TodayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.Today);
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void CalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.Calendar);
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void PreviousMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowPreviousMonth();
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void NextMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowNextMonth();
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void CurrentMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ReturnToCurrentMonth();
        _ = _viewModel.LoadAsync();
        _ = _coordinator.SavePreferencesAsync();
    }

    private void DateCell_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.SetHoverDate(date);
        }
    }

    private void DateCell_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.ClearHoverDate(date);
        }
    }

    private void DateCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.PinDate(date);
        }
    }

    private async void QuickCompleteTodoCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: TodoItem todo } checkBox)
        {
            return;
        }

        await _viewModel.SetCompletedAsync(todo, checkBox.IsChecked == true);
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isHidingFromButton = true;
        _coordinator.HideWidget();
        _isHidingFromButton = false;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isHidingFromButton)
        {
            return;
        }

        _ = _coordinator.SavePreferencesAsync();
    }
}
