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
    private TodoItem? _editingTodo;
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
            _viewModel.OpenPopoverForDate(date);
            ShowPopover();
        }
    }

    private void DateCell_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.ClearHoverDate(date);
            if (_viewModel.PinnedDate is null)
            {
                EditPopover.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void DateCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DateOnly date)
        {
            _viewModel.PinDate(date);
            ShowPopover();
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

    private void ShowPopover()
    {
        EditPopover.Visibility = Visibility.Visible;
        PopoverErrorTextBlock.Visibility = Visibility.Collapsed;
        PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
    }

    private void ClosePopoverButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearPinnedDate();
        EditPopover.Visibility = Visibility.Collapsed;
    }

    private async void AddTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var draft = CreateDraftFromPopover();
            if (_editingTodo is null)
            {
                await _viewModel.CreateTodoAsync(draft);
            }
            else
            {
                await _viewModel.UpdateTodoAsync(_editingTodo, draft);
            }

            ClearTodoForm();
            PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
            PopoverErrorTextBlock.Visibility = Visibility.Collapsed;
            await _coordinator.SavePreferencesAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            PopoverErrorTextBlock.Text = ex.Message;
            PopoverErrorTextBlock.Visibility = Visibility.Visible;
        }
    }

    private TodoDraft CreateDraftFromPopover()
    {
        var timeKind = TimeKindComboBox.SelectedIndex switch
        {
            1 => TodoTimeKind.Single,
            2 => TodoTimeKind.Range,
            _ => TodoTimeKind.None
        };

        TimeOnly? startTime = string.IsNullOrWhiteSpace(StartTimeTextBox.Text)
            ? null
            : TimeOnly.Parse(StartTimeTextBox.Text);
        TimeOnly? endTime = string.IsNullOrWhiteSpace(EndTimeTextBox.Text)
            ? null
            : TimeOnly.Parse(EndTimeTextBox.Text);

        return new TodoDraft(_viewModel.ActivePopoverDate, TodoTextBox.Text, timeKind, startTime, endTime);
    }

    private async void CompleteTodoCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
        {
            return;
        }

        await _viewModel.SetCompletedAsync(todo, !todo.IsCompleted);
        PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
    }

    private void EditTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
        {
            return;
        }

        _editingTodo = todo;
        TodoTextBox.Text = todo.Text;
        TimeKindComboBox.SelectedIndex = todo.StartTime is null ? 0 : todo.EndTime is null ? 1 : 2;
        StartTimeTextBox.Text = todo.StartTime?.ToString("HH:mm") ?? string.Empty;
        EndTimeTextBox.Text = todo.EndTime?.ToString("HH:mm") ?? string.Empty;
        SaveTodoButton.Content = "保存";
    }

    private async void DeleteTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TodoItem todo)
        {
            return;
        }

        await _viewModel.DeleteTodoAsync(todo);
        if (_editingTodo?.Id == todo.Id)
        {
            ClearTodoForm();
        }

        PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
    }

    private void ClearTodoFormButton_OnClick(object sender, RoutedEventArgs e)
    {
        ClearTodoForm();
    }

    private void ClearTodoForm()
    {
        _editingTodo = null;
        TodoTextBox.Text = string.Empty;
        TimeKindComboBox.SelectedIndex = 0;
        StartTimeTextBox.Text = string.Empty;
        EndTimeTextBox.Text = string.Empty;
        SaveTodoButton.Content = "新增";
    }
}
