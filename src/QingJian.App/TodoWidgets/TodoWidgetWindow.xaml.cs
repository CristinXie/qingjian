using System.ComponentModel;
using System.Runtime.InteropServices;
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
    public static readonly DependencyProperty PopoverEditorVisibilityProperty =
        DependencyProperty.Register(
            nameof(PopoverEditorVisibility),
            typeof(Visibility),
            typeof(TodoWidgetWindow),
            new PropertyMetadata(Visibility.Visible));

    private readonly TodoWidgetViewModel _viewModel;
    private readonly WindowZOrderService _windowZOrderService;
    private readonly TodoWidgetCoordinator _coordinator;
    private readonly DispatcherTimer _minimizeRecoveryTimer;
    private readonly DispatcherTimer _calendarHoverPreviewTimer;
    private readonly DispatcherTimer _transientPopoverFocusTimer;
    private ThreadingTimer? _desktopOwnerAttachTimer;
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private TodoItem? _editingTodo;
    private FrameworkElement? _editPopoverAnchor;
    private FrameworkElement? _calendarHoverAnchor;
    private DateOnly? _calendarHoverDate;
    private IntPtr _foregroundWindowWhenPopoverOpened;
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

        _calendarHoverPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _calendarHoverPreviewTimer.Tick += CalendarHoverPreviewTimer_OnTick;

        _transientPopoverFocusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(125)
        };
        _transientPopoverFocusTimer.Tick += TransientPopoverFocusTimer_OnTick;
    }

    public Visibility PopoverEditorVisibility
    {
        get => (Visibility)GetValue(PopoverEditorVisibilityProperty);
        set => SetValue(PopoverEditorVisibilityProperty, value);
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

    private void Window_OnDeactivated(object? sender, EventArgs e)
    {
        CloseTransientPopovers();
    }

    private void TransientPopoverFocusTimer_OnTick(object? sender, EventArgs e)
    {
        CloseTransientPopoversIfFocusLost();
    }

    private void CloseTransientPopoversIfFocusLost()
    {
        if (!HasOpenTransientPopover())
        {
            _transientPopoverFocusTimer.Stop();
            return;
        }

        var foregroundWindow = GetForegroundWindow();
        var pointerOverCalendarPreview =
            _calendarHoverAnchor?.IsMouseOver == true || CalendarPreviewPopover.IsMouseOver;
        if (foregroundWindow == _windowHandle)
        {
            _foregroundWindowWhenPopoverOpened = foregroundWindow;
            return;
        }

        if (foregroundWindow != _foregroundWindowWhenPopoverOpened ||
            (foregroundWindow != _windowHandle && !IsMouseOver && !pointerOverCalendarPreview))
        {
            CloseTransientPopovers();
        }
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

    private async void CycleModeButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseTransientPopovers();
        _viewModel.CycleMode();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void PreviousMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseTransientPopovers();
        _viewModel.ShowPreviousMonth();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void NextMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseTransientPopovers();
        _viewModel.ShowNextMonth();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void CurrentMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseTransientPopovers();
        _viewModel.ReturnToCurrentMonth();
        await _viewModel.LoadAsync();
        await _coordinator.SavePreferencesAsync();
    }

    private async void LockButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsLocked = !_viewModel.IsLocked;
        await _coordinator.SavePreferencesAsync();
    }

    private void AddTodayTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor)
        {
            return;
        }

        CloseTransientPopovers();
        _viewModel.PinToday();
        ShowQuickAddPopover(anchor);
    }

    private void EditTodayTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TodoItem todo })
        {
            return;
        }

        CloseTransientPopovers();
        _viewModel.PinDate(todo.Date);
        ShowTodoManagePopover(anchor: null, showEditor: false);
        BeginEditingTodo(todo);
    }

    private void DateCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DateOnly date } anchor)
        {
            return;
        }

        if (TodoWidgetManagePopoverToggle.ShouldClose(
                EditPopover.Visibility == Visibility.Visible,
                _viewModel.PinnedDate,
                date))
        {
            CloseEditPopover();
            return;
        }

        CloseQuickAddPopover();
        CloseCalendarPreviewPopover();
        _viewModel.PinDate(date);
        ShowTodoManagePopover(anchor, showEditor: false);
    }

    private void AddTodoForDateButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DateOnly date } anchor)
        {
            CloseEditPopover();
            CloseCalendarPreviewPopover();
            _viewModel.PinDate(date);
            ShowQuickAddPopover(anchor);
        }

        e.Handled = true;
    }

    private void CalendarDateCell_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_viewModel.Mode != TodoWidgetMode.Calendar ||
            EditPopover.Visibility == Visibility.Visible ||
            QuickAddPopover.Visibility == Visibility.Visible ||
            sender is not FrameworkElement { DataContext: DateOnly date } anchor)
        {
            return;
        }

        _calendarHoverPreviewTimer.Stop();
        if (CalendarPreviewPopover.Visibility == Visibility.Visible && _calendarHoverDate != date)
        {
            CloseCalendarPreviewPopover();
        }

        _calendarHoverAnchor = anchor;
        _calendarHoverDate = date;
        _calendarHoverPreviewTimer.Start();
    }

    private void CalendarDateCell_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!ReferenceEquals(sender, _calendarHoverAnchor))
        {
            return;
        }

        if (CalendarPreviewPopover.Visibility != Visibility.Visible)
        {
            CloseCalendarPreviewPopover();
            return;
        }

        Dispatcher.BeginInvoke((Action)CloseCalendarPreviewIfPointerLeft, DispatcherPriority.Input);
    }

    private void CalendarPreviewPopover_OnMouseLeave(object sender, MouseEventArgs e)
    {
        Dispatcher.BeginInvoke((Action)CloseCalendarPreviewIfPointerLeft, DispatcherPriority.Input);
    }

    private void CalendarHoverPreviewTimer_OnTick(object? sender, EventArgs e)
    {
        _calendarHoverPreviewTimer.Stop();
        if (_viewModel.Mode != TodoWidgetMode.Calendar ||
            _calendarHoverAnchor?.IsMouseOver != true ||
            _calendarHoverDate is null ||
            EditPopover.Visibility == Visibility.Visible ||
            QuickAddPopover.Visibility == Visibility.Visible)
        {
            return;
        }

        ShowCalendarPreviewPopover(_calendarHoverAnchor);
    }

    private void CloseCalendarPreviewIfPointerLeft()
    {
        if (_calendarHoverAnchor?.IsMouseOver == true || CalendarPreviewPopover.IsMouseOver)
        {
            return;
        }

        CloseCalendarPreviewPopover();
    }

    private async void QuickCompleteTodoCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: TodoItem todo } checkBox)
        {
            return;
        }

        await _viewModel.SetCompletedAsync(todo, checkBox.IsChecked == true);
        RefreshCalendarPreviewTodos();
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
        _calendarHoverPreviewTimer.Stop();
        _transientPopoverFocusTimer.Stop();
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

    private void ShowTodoManagePopover(FrameworkElement? anchor, bool showEditor)
    {
        _editPopoverAnchor = anchor;
        SetPopoverEditorVisible(showEditor);
        EditPopover.Visibility = Visibility.Visible;
        ResetEditValidationState();
        PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
        var contentTop = MainLayoutGrid.RowDefinitions[0].ActualHeight;
        var availableSize = new Size(
            MainLayoutGrid.ActualWidth,
            Math.Max(0, MainLayoutGrid.ActualHeight - contentTop));
        var quickAddHeight = MeasureQuickAddPopoverSize().Height;
        EditPopover.Height = Math.Min(quickAddHeight, availableSize.Height);
        EditPopover.MaxHeight = availableSize.Height;
        if (anchor is null)
        {
            EditPopover.Margin = new Thickness(0, 24, 0, 0);
        }
        else
        {
            PositionEditPopoverNear(anchor);
        }

        RegisterTransientPopoverOpened();
    }

    private void SetPopoverEditorVisible(bool isVisible)
    {
        PopoverEditorVisibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        PopoverTodoListRow.Height = isVisible
            ? GridLength.Auto
            : new GridLength(1, GridUnitType.Star);
        PopoverEditorRow.Height = isVisible
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        PopoverTodoListBox.MaxHeight = isVisible ? 72 : double.PositiveInfinity;
    }

    private void PositionEditPopoverNear(FrameworkElement anchor)
    {
        var contentTop = MainLayoutGrid.RowDefinitions[0].ActualHeight;
        var availableSize = new Size(
            MainLayoutGrid.ActualWidth,
            Math.Max(0, MainLayoutGrid.ActualHeight - contentTop));
        EditPopover.MaxHeight = availableSize.Height;
        var anchorTopLeft = anchor.TransformToAncestor(MainLayoutGrid).Transform(new Point(0, 0));
        var anchorBounds = new Rect(
            anchorTopLeft.X,
            anchorTopLeft.Y - contentTop,
            anchor.ActualWidth,
            anchor.ActualHeight);
        var popoverSize = MeasureEditPopoverSize();
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            anchorBounds,
            popoverSize,
            availableSize,
            gap: 8);

        EditPopover.Margin = new Thickness(position.Left, position.Top, 0, 0);
    }

    private Size MeasureEditPopoverSize()
    {
        var previousVisibility = EditPopover.Visibility;
        if (previousVisibility == Visibility.Collapsed)
        {
            EditPopover.Visibility = Visibility.Hidden;
        }

        EditPopover.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = double.IsNaN(EditPopover.Width)
            ? EditPopover.DesiredSize.Width
            : EditPopover.Width;
        var size = new Size(width, EditPopover.DesiredSize.Height);
        EditPopover.Visibility = previousVisibility;
        return size;
    }

    private void ShowCalendarPreviewPopover(FrameworkElement anchor)
    {
        RefreshCalendarPreviewTodos();
        PositionCalendarPreviewPopoverNear(anchor);
        CalendarPreviewPopover.Visibility = Visibility.Visible;
        RegisterTransientPopoverOpened();
    }

    private void PositionCalendarPreviewPopoverNear(FrameworkElement anchor)
    {
        var contentTop = MainLayoutGrid.RowDefinitions[0].ActualHeight;
        var availableSize = new Size(
            MainLayoutGrid.ActualWidth,
            Math.Max(0, MainLayoutGrid.ActualHeight - contentTop));
        CalendarPreviewPopover.MaxHeight = availableSize.Height;
        var anchorTopLeft = anchor.TransformToAncestor(MainLayoutGrid).Transform(new Point(0, 0));
        var anchorBounds = new Rect(
            anchorTopLeft.X,
            anchorTopLeft.Y - contentTop,
            anchor.ActualWidth,
            anchor.ActualHeight);
        var popoverSize = MeasureCalendarPreviewPopoverSize();
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            anchorBounds,
            popoverSize,
            availableSize,
            gap: -6);

        CalendarPreviewPopover.Margin = new Thickness(position.Left, position.Top, 0, 0);
    }

    private Size MeasureCalendarPreviewPopoverSize()
    {
        var previousVisibility = CalendarPreviewPopover.Visibility;
        var previousMargin = CalendarPreviewPopover.Margin;
        if (previousVisibility == Visibility.Collapsed)
        {
            CalendarPreviewPopover.Visibility = Visibility.Hidden;
        }

        CalendarPreviewPopover.Margin = new Thickness(0);
        CalendarPreviewPopover.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = double.IsNaN(CalendarPreviewPopover.Width)
            ? CalendarPreviewPopover.DesiredSize.Width
            : CalendarPreviewPopover.Width;
        var size = new Size(width, CalendarPreviewPopover.DesiredSize.Height);
        CalendarPreviewPopover.Margin = previousMargin;
        CalendarPreviewPopover.Visibility = previousVisibility;
        return size;
    }

    private void RefreshCalendarPreviewTodos()
    {
        if (_calendarHoverDate is not { } date)
        {
            return;
        }

        CalendarPreviewTodoListBox.ItemsSource = _viewModel.TodosForDate(date);
    }

    private void ClosePopoverButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseEditPopover();
    }

    private void CloseEditPopover()
    {
        _viewModel.ClearPinnedDate();
        _editPopoverAnchor = null;
        EditPopover.Visibility = Visibility.Collapsed;
        SetPopoverEditorVisible(false);
        ClearTodoForm();
        ResetEditValidationState();
        StopTransientPopoverFocusMonitoringIfIdle();
    }

    private async void AddTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        var validation = TodoWidgetDraftParser.ValidateQuickAddDraft(
            _viewModel.ActivePopoverDate,
            TodoTextBox.Text,
            EditStartHourTextBox.Text,
            EditStartMinuteTextBox.Text,
            EditEndHourTextBox.Text,
            EditEndMinuteTextBox.Text);

        ApplyEditValidationState(validation);
        if (!validation.IsValid || validation.Draft is null)
        {
            return;
        }

        try
        {
            if (_editingTodo is null)
            {
                await _viewModel.CreateTodoAsync(validation.Draft);
            }
            else
            {
                await _viewModel.UpdateTodoAsync(_editingTodo, validation.Draft);
            }

            ClearTodoForm();
            PopoverTodoListBox.ItemsSource = _viewModel.TodosForDate(_viewModel.ActivePopoverDate);
            PopoverErrorTextBlock.Visibility = Visibility.Collapsed;
            SetPopoverEditorVisible(false);
            await _coordinator.SavePreferencesAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            PopoverErrorTextBlock.Text = ex.Message;
            PopoverErrorTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void ShowQuickAddPopover(FrameworkElement anchor)
    {
        ClearQuickAddForm();
        ResetQuickAddValidationState();
        PositionQuickAddPopoverNear(anchor);
        QuickAddPopover.Visibility = Visibility.Visible;
        RegisterTransientPopoverOpened();
    }

    private void PositionQuickAddPopoverNear(FrameworkElement anchor)
    {
        var contentTop = MainLayoutGrid.RowDefinitions[0].ActualHeight;
        var anchorTopLeft = anchor.TransformToAncestor(MainLayoutGrid).Transform(new Point(0, 0));
        var anchorBounds = new Rect(
            anchorTopLeft.X,
            anchorTopLeft.Y - contentTop,
            anchor.ActualWidth,
            anchor.ActualHeight);
        var popoverSize = MeasureQuickAddPopoverSize();
        var availableSize = new Size(
            MainLayoutGrid.ActualWidth,
            Math.Max(0, MainLayoutGrid.ActualHeight - contentTop));
        var position = TodoWidgetPopoverPositioner.CalculateNearAnchor(
            anchorBounds,
            popoverSize,
            availableSize,
            gap: 8);

        QuickAddPopover.Margin = new Thickness(position.Left, position.Top, 0, 0);
    }

    private Size MeasureQuickAddPopoverSize()
    {
        var previousVisibility = QuickAddPopover.Visibility;
        if (previousVisibility == Visibility.Collapsed)
        {
            QuickAddPopover.Visibility = Visibility.Hidden;
        }

        QuickAddPopover.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = double.IsNaN(QuickAddPopover.Width)
            ? QuickAddPopover.DesiredSize.Width
            : QuickAddPopover.Width;
        var size = new Size(width, QuickAddPopover.DesiredSize.Height);
        QuickAddPopover.Visibility = previousVisibility;
        return size;
    }

    private void CloseQuickAddPopoverButton_OnClick(object sender, RoutedEventArgs e)
    {
        CloseQuickAddPopover();
    }

    private async void CreateQuickAddTodoButton_OnClick(object sender, RoutedEventArgs e)
    {
        var validation = TodoWidgetDraftParser.ValidateQuickAddDraft(
            _viewModel.ActivePopoverDate,
            QuickAddTextBox.Text,
            QuickAddStartHourTextBox.Text,
            QuickAddStartMinuteTextBox.Text,
            QuickAddEndHourTextBox.Text,
            QuickAddEndMinuteTextBox.Text);

        ApplyQuickAddValidationState(validation);
        if (!validation.IsValid || validation.Draft is null)
        {
            return;
        }

        await _viewModel.CreateTodoAsync(validation.Draft);
        HideQuickAddPopoverAfterCreate();
        await _coordinator.SavePreferencesAsync();
    }

    private void HideQuickAddPopoverAfterCreate()
    {
        CloseQuickAddPopover();
    }

    private void CloseQuickAddPopover()
    {
        _viewModel.ClearPinnedDate();
        QuickAddPopover.Visibility = Visibility.Collapsed;
        ClearQuickAddForm();
        ResetQuickAddValidationState();
        StopTransientPopoverFocusMonitoringIfIdle();
    }

    private void CloseCalendarPreviewPopover()
    {
        _calendarHoverPreviewTimer.Stop();
        CalendarPreviewPopover.Visibility = Visibility.Collapsed;
        CalendarPreviewTodoListBox.ItemsSource = null;
        _calendarHoverAnchor = null;
        _calendarHoverDate = null;
        StopTransientPopoverFocusMonitoringIfIdle();
    }

    private void CloseTransientPopovers()
    {
        CloseEditPopover();
        CloseQuickAddPopover();
        CloseCalendarPreviewPopover();
        _transientPopoverFocusTimer.Stop();
    }

    private void RegisterTransientPopoverOpened()
    {
        _foregroundWindowWhenPopoverOpened = GetForegroundWindow();
        _transientPopoverFocusTimer.Start();
    }

    private bool HasOpenTransientPopover()
    {
        return EditPopover.Visibility == Visibility.Visible ||
            QuickAddPopover.Visibility == Visibility.Visible ||
            CalendarPreviewPopover.Visibility == Visibility.Visible;
    }

    private void StopTransientPopoverFocusMonitoringIfIdle()
    {
        if (!HasOpenTransientPopover())
        {
            _transientPopoverFocusTimer.Stop();
        }
    }

    private void QuickAddField_OnChanged(object sender, RoutedEventArgs e)
    {
        ResetQuickAddValidationState();
    }

    private void ApplyQuickAddValidationState(TodoWidgetQuickAddDraftValidation validation)
    {
        var borderBrush = GetWidgetBorderBrush();
        var dangerBrush = GetWidgetDangerBrush();

        QuickAddTextBox.BorderBrush = validation.TextError is null ? borderBrush : dangerBrush;
        QuickAddTextErrorTextBlock.Text = validation.TextError ?? string.Empty;
        QuickAddTextErrorTextBlock.Visibility = validation.TextError is null ? Visibility.Collapsed : Visibility.Visible;

        QuickAddTimePickerBorder.BorderBrush = validation.TimeError is null ? borderBrush : dangerBrush;
        QuickAddTimeErrorTextBlock.Text = validation.TimeError ?? string.Empty;
        QuickAddTimeErrorTextBlock.Visibility = validation.TimeError is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ResetQuickAddValidationState()
    {
        QuickAddTextBox.BorderBrush = GetWidgetBorderBrush();
        QuickAddTextErrorTextBlock.Text = string.Empty;
        QuickAddTextErrorTextBlock.Visibility = Visibility.Collapsed;
        QuickAddTimePickerBorder.BorderBrush = GetWidgetBorderBrush();
        QuickAddTimeErrorTextBlock.Text = string.Empty;
        QuickAddTimeErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void EditField_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        ResetEditValidationState();
    }

    private void ApplyEditValidationState(TodoWidgetQuickAddDraftValidation validation)
    {
        var borderBrush = GetWidgetBorderBrush();
        var dangerBrush = GetWidgetDangerBrush();

        TodoTextBox.BorderBrush = validation.TextError is null ? borderBrush : dangerBrush;
        EditTimePickerBorder.BorderBrush = validation.TimeError is null ? borderBrush : dangerBrush;

        var errors = new[] { validation.TextError, validation.TimeError }
            .Where(error => error is not null);
        PopoverErrorTextBlock.Text = string.Join("；", errors);
        PopoverErrorTextBlock.Visibility = validation.IsValid ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ResetEditValidationState()
    {
        TodoTextBox.BorderBrush = GetWidgetBorderBrush();
        EditTimePickerBorder.BorderBrush = GetWidgetBorderBrush();
        PopoverErrorTextBlock.Text = string.Empty;
        PopoverErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void ClearQuickAddForm()
    {
        QuickAddTextBox.Text = string.Empty;
        QuickAddStartHourTextBox.Text = string.Empty;
        QuickAddStartMinuteTextBox.Text = string.Empty;
        QuickAddEndHourTextBox.Text = string.Empty;
        QuickAddEndMinuteTextBox.Text = string.Empty;
    }

    private Brush GetWidgetBorderBrush()
    {
        return (Brush)FindResource("TodoWidgetBorderBrush");
    }

    private Brush GetWidgetDangerBrush()
    {
        return (Brush)FindResource("TodoWidgetDangerBrush");
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

        BeginEditingTodo(todo);
    }

    private void BeginEditingTodo(TodoItem todo)
    {
        _editingTodo = todo;
        SetPopoverEditorVisible(true);
        TodoTextBox.Text = todo.Text;
        EditStartHourTextBox.Text = todo.StartTime?.ToString("HH") ?? string.Empty;
        EditStartMinuteTextBox.Text = todo.StartTime?.ToString("mm") ?? string.Empty;
        EditEndHourTextBox.Text = todo.EndTime?.ToString("HH") ?? string.Empty;
        EditEndMinuteTextBox.Text = todo.EndTime?.ToString("mm") ?? string.Empty;
        SaveTodoButton.Content = "保存";

        if (_editPopoverAnchor is FrameworkElement anchor)
        {
            Dispatcher.BeginInvoke(
                () =>
                {
                    if (EditPopover.Visibility == Visibility.Visible && ReferenceEquals(_editPopoverAnchor, anchor))
                    {
                        PositionEditPopoverNear(anchor);
                    }
                },
                DispatcherPriority.Loaded);
        }
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
        EditStartHourTextBox.Text = string.Empty;
        EditStartMinuteTextBox.Text = string.Empty;
        EditEndHourTextBox.Text = string.Empty;
        EditEndMinuteTextBox.Text = string.Empty;
        SaveTodoButton.Content = "新增";
        ResetEditValidationState();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
