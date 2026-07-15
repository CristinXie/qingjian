using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using QingJian.App.Models;
using ThreadingTimer = System.Threading.Timer;

namespace QingJian.App.TodoWidgets;

public partial class TodoWidgetWindow : Window, ITodoWidgetWindow
{
    private readonly TodoWidgetViewModel _viewModel;
    private readonly WindowZOrderService _windowZOrderService;
    private readonly TodoWidgetCoordinator _coordinator;
    private readonly DispatcherTimer _minimizeRecoveryTimer;
    private ThreadingTimer? _desktopOwnerAttachTimer;
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private TodoItem? _editingTodo;
    private bool _isHidingFromButton;
    private bool _isRestoringFromSystemMinimize;
    private bool _isDragging;
    private Point _dragStartScreenPosition;
    private double _dragStartLeft;
    private double _dragStartTop;

    public TodoWidgetWindow(
        TodoWidgetViewModel viewModel,
        WindowZOrderService windowZOrderService,
        TodoWidgetCoordinator coordinator)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _windowZOrderService = windowZOrderService;
        _coordinator = coordinator;
        DataContext = _viewModel;

        _minimizeRecoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _minimizeRecoveryTimer.Tick += MinimizeRecoveryTimer_OnTick;
        _minimizeRecoveryTimer.Start();

    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        _windowHandle = _hwndSource?.Handle ?? IntPtr.Zero;
        _hwndSource?.AddHook(WndProc);
        Topmost = false;
        _windowZOrderService.AttachToDesktopOwner(this);
        MoveBehindOtherWindows();
        _desktopOwnerAttachTimer = new ThreadingTimer(
            DesktopOwnerAttachTimer_OnTick,
            null,
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(2));
    }

    private void DesktopOwnerAttachTimer_OnTick(object? state)
    {
        _windowZOrderService.AttachToDesktopOwner(_windowHandle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (TodoWidgetWindowMessageFilter.ShouldBlockMinimize(msg, wParam, _isHidingFromButton))
        {
            handled = true;
            MoveBehindOtherWindows();
        }

        return IntPtr.Zero;
    }

    private void Window_OnStateChanged(object? sender, EventArgs e)
    {
        if (_isRestoringFromSystemMinimize ||
            !TodoWidgetMinimizeRestorer.ShouldRestore(WindowState, _isHidingFromButton, IsVisible))
        {
            return;
        }

        _isRestoringFromSystemMinimize = true;
        Dispatcher.BeginInvoke(RestoreFromSystemMinimize, DispatcherPriority.Background);
    }

    private void MinimizeRecoveryTimer_OnTick(object? sender, EventArgs e)
    {
        if (_isRestoringFromSystemMinimize ||
            !TodoWidgetMinimizeRestorer.ShouldRestore(WindowState, _isHidingFromButton, IsVisible))
        {
            return;
        }

        RestoreFromSystemMinimize();
    }

    private void RestoreFromSystemMinimize()
    {
        WindowState = WindowState.Normal;
        Show();
        MoveBehindOtherWindows();
        _isRestoringFromSystemMinimize = false;
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

        _isDragging = true;
        _dragStartScreenPosition = PointToScreen(e.GetPosition(this));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || _viewModel.IsLocked)
        {
            EndDrag(sender);
            return;
        }

        var currentPosition = PointToScreen(e.GetPosition(this));
        var windowPosition = TodoWidgetDragCalculator.CalculateWindowPosition(
            _dragStartLeft,
            _dragStartTop,
            _dragStartScreenPosition,
            currentPosition,
            GetTransformFromDevice());
        Left = windowPosition.X;
        Top = windowPosition.Y;
    }

    private void DragHandle_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        EndDrag(sender);
        _ = _coordinator.SavePreferencesAsync();
        MoveBehindOtherWindows();
    }

    private void EndDrag(object sender)
    {
        _isDragging = false;
        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }
    }

    public void MoveBehindOtherWindows()
    {
        _windowZOrderService.MoveBehindOtherWindows(this);
    }

    private Matrix GetTransformFromDevice()
    {
        return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
    }

    private async void EightDayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.EightDay);
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void TodayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.Today);
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void CalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetMode(TodoWidgetMode.Calendar);
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void PreviousMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowPreviousMonth();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void NextMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowNextMonth();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void CurrentMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ReturnToCurrentMonth();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void LockCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        await _coordinator.SavePreferencesAsync();
    }

    private void OpenTodayPopoverButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.PinToday();
        ShowPopover();
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

    private async void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isHidingFromButton = true;
        await _coordinator.HideWidgetAsync();
        _isHidingFromButton = false;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        _minimizeRecoveryTimer.Stop();
        _desktopOwnerAttachTimer?.Dispose();
        _desktopOwnerAttachTimer = null;
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        _windowHandle = IntPtr.Zero;

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
        return TodoWidgetDraftParser.CreateDraft(
            _viewModel.ActivePopoverDate,
            TodoTextBox.Text,
            TimeKindComboBox.SelectedIndex,
            StartTimeTextBox.Text,
            EndTimeTextBox.Text);
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
