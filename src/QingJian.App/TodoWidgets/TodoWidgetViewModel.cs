using System.Collections.ObjectModel;
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.ViewModels;

namespace QingJian.App.TodoWidgets;

public sealed class TodoWidgetViewModel : ViewModelBase
{
    private readonly ITodoService _todoService;
    private readonly Func<DateOnly> _todayProvider;
    private TodoWidgetMode _mode;
    private DateOnly _calendarMonth;
    private DateOnly? _hoverDate;
    private DateOnly? _pinnedDate;
    private bool _isLocked;
    private double _opacity;

    public TodoWidgetViewModel(ITodoService todoService, TodoWidgetPreferences preferences)
        : this(todoService, preferences, () => DateOnly.FromDateTime(DateTime.Today))
    {
    }

    public TodoWidgetViewModel(
        ITodoService todoService,
        TodoWidgetPreferences preferences,
        Func<DateOnly> todayProvider)
    {
        _todoService = todoService;
        _todayProvider = todayProvider;
        var normalized = preferences.Normalize();
        _mode = normalized.Mode;
        _calendarMonth = new DateOnly(normalized.CalendarYear, normalized.CalendarMonth, 1);
        _isLocked = normalized.IsLocked;
        _opacity = normalized.Opacity;
    }

    public ObservableCollection<TodoItem> VisibleTodos { get; } = new();

    public TodoWidgetMode Mode
    {
        get => _mode;
        private set => SetField(ref _mode, value);
    }

    public DateOnly CalendarMonth
    {
        get => _calendarMonth;
        private set => SetField(ref _calendarMonth, value);
    }

    public DateOnly? HoverDate
    {
        get => _hoverDate;
        private set => SetField(ref _hoverDate, value);
    }

    public DateOnly? PinnedDate
    {
        get => _pinnedDate;
        private set => SetField(ref _pinnedDate, value);
    }

    public bool IsLocked
    {
        get => _isLocked;
        set => SetField(ref _isLocked, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, Math.Clamp(value, 0.35, 0.95));
    }

    public IReadOnlyList<DateOnly> EightDayDates
    {
        get
        {
            var start = _todayProvider().AddDays(-1);
            return Enumerable.Range(0, 8).Select(start.AddDays).ToList();
        }
    }

    public IReadOnlyList<DateOnly> CalendarDates
    {
        get
        {
            var first = CalendarMonth;
            var leading = ((int)first.DayOfWeek + 6) % 7;
            var start = first.AddDays(-leading);
            return Enumerable.Range(0, 42).Select(start.AddDays).ToList();
        }
    }

    public DateOnly ActivePopoverDate => PinnedDate ?? HoverDate ?? _todayProvider();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _todoService.InitializeAsync(cancellationToken);
        var (from, to) = GetVisibleRange();
        var todos = await _todoService.GetTodosAsync(from, to, cancellationToken);

        VisibleTodos.Clear();
        foreach (var todo in todos)
        {
            VisibleTodos.Add(todo);
        }
    }

    public async Task CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
    {
        await _todoService.CreateTodoAsync(draft, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
    {
        await _todoService.UpdateTodoAsync(todo, draft, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task SetCompletedAsync(TodoItem todo, bool completed, CancellationToken cancellationToken = default)
    {
        await _todoService.SetCompletedAsync(todo, completed, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public async Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        await _todoService.DeleteTodoAsync(todo, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    public void SetMode(TodoWidgetMode mode)
    {
        Mode = mode;
        OnPropertyChanged(nameof(EightDayDates));
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void ShowPreviousMonth()
    {
        CalendarMonth = CalendarMonth.AddMonths(-1);
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void ShowNextMonth()
    {
        CalendarMonth = CalendarMonth.AddMonths(1);
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void ReturnToCurrentMonth()
    {
        var today = _todayProvider();
        CalendarMonth = new DateOnly(today.Year, today.Month, 1);
        OnPropertyChanged(nameof(CalendarDates));
    }

    public void SetHoverDate(DateOnly date)
    {
        HoverDate = date;
    }

    public void OpenPopoverForDate(DateOnly date)
    {
        HoverDate = date;
        OnPropertyChanged(nameof(ActivePopoverDate));
    }

    public void ClearHoverDate(DateOnly date)
    {
        if (HoverDate == date)
        {
            HoverDate = null;
        }
    }

    public void PinDate(DateOnly date)
    {
        PinnedDate = date;
        OnPropertyChanged(nameof(ActivePopoverDate));
    }

    public void PinToday()
    {
        PinDate(_todayProvider());
    }

    public void ClearPinnedDate()
    {
        PinnedDate = null;
        OnPropertyChanged(nameof(ActivePopoverDate));
    }

    public TodoWidgetPreferences ToPreferences(double left, double top, bool isVisible)
    {
        return new TodoWidgetPreferences(
            IsVisible: isVisible,
            Mode: Mode,
            Left: left,
            Top: top,
            Opacity: Opacity,
            IsLocked: IsLocked,
            CalendarYear: CalendarMonth.Year,
            CalendarMonth: CalendarMonth.Month).Normalize();
    }

    public IReadOnlyList<TodoItem> TodosForDate(DateOnly date)
    {
        return _todoService.SortTodos(VisibleTodos.Where(todo => todo.Date == date));
    }

    private (DateOnly From, DateOnly To) GetVisibleRange()
    {
        return Mode switch
        {
            TodoWidgetMode.EightDay => (EightDayDates[0], EightDayDates[^1]),
            TodoWidgetMode.Today => (_todayProvider(), _todayProvider()),
            TodoWidgetMode.Calendar => (CalendarMonth, CalendarMonth.AddMonths(1).AddDays(-1)),
            _ => (_todayProvider(), _todayProvider())
        };
    }
}
