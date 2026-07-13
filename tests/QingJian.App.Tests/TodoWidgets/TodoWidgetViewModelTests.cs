using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetViewModelTests
{
    [Fact]
    public async Task LoadAsync_InEightDayMode_LoadsYesterdayThroughSixDaysAfterToday()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        await viewModel.LoadAsync();

        Assert.Equal(new DateOnly(2026, 7, 12), viewModel.EightDayDates[0]);
        Assert.Equal(new DateOnly(2026, 7, 19), viewModel.EightDayDates[^1]);
        Assert.Equal((new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 19)), service.LastRange);
    }

    [Fact]
    public async Task SetMode_Today_LoadsCurrentDateOnly()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        viewModel.SetMode(TodoWidgetMode.Today);
        await viewModel.LoadAsync();

        Assert.Equal(TodoWidgetMode.Today, viewModel.Mode);
        Assert.Equal((today, today), service.LastRange);
    }

    [Fact]
    public async Task CalendarNavigation_LoadsVisibleCalendarGridAndReturnsToCurrentMonth()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(
            service,
            TodoWidgetPreferences.Default with { Mode = TodoWidgetMode.Calendar },
            () => today);

        viewModel.ShowNextMonth();
        await viewModel.LoadAsync();
        Assert.Equal(8, viewModel.CalendarMonth.Month);
        Assert.Equal(new DateOnly(2026, 7, 27), service.LastRange.From);
        Assert.Equal(new DateOnly(2026, 9, 6), service.LastRange.To);

        viewModel.ReturnToCurrentMonth();
        await viewModel.LoadAsync();
        Assert.Equal(7, viewModel.CalendarMonth.Month);
    }

    [Fact]
    public async Task LoadAsync_NotifiesVisibleTodosAfterReload()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        await viewModel.LoadAsync();

        Assert.Contains(nameof(TodoWidgetViewModel.VisibleTodos), changedProperties);
    }

    [Fact]
    public async Task CreateTodoAsync_CreatesTodoAndReloadsVisibleRange()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        await viewModel.CreateTodoAsync(new TodoDraft(today, "Task", TodoTimeKind.None, null, null));

        Assert.Single(service.CreatedDrafts);
        Assert.Single(viewModel.VisibleTodos);
    }

    [Fact]
    public async Task TodosForDate_ReturnsOnlySelectedDateTodos()
    {
        var today = new DateOnly(2026, 7, 13);
        var service = new InMemoryTodoService();
        var viewModel = new TodoWidgetViewModel(service, TodoWidgetPreferences.Default, () => today);

        await viewModel.CreateTodoAsync(new TodoDraft(today, "Today", TodoTimeKind.None, null, null));
        await viewModel.CreateTodoAsync(new TodoDraft(today.AddDays(1), "Tomorrow", TodoTimeKind.None, null, null));

        var todos = viewModel.TodosForDate(today);

        Assert.Single(todos);
        Assert.Equal("Today", todos[0].Text);
    }

    [Fact]
    public void PinAndHoverDates_TrackPopoverTarget()
    {
        var today = new DateOnly(2026, 7, 13);
        var viewModel = new TodoWidgetViewModel(new InMemoryTodoService(), TodoWidgetPreferences.Default, () => today);

        viewModel.SetHoverDate(today);
        viewModel.PinDate(today);
        viewModel.ClearHoverDate(today);

        Assert.Equal(today, viewModel.PinnedDate);
        Assert.Null(viewModel.HoverDate);

        viewModel.ClearPinnedDate();
        Assert.Null(viewModel.PinnedDate);
    }

    [Fact]
    public void HoverPopoverTargetDate_ClearsAfterHoverLeaves()
    {
        var today = new DateOnly(2026, 7, 13);
        var selectedDate = today.AddDays(2);
        var viewModel = new TodoWidgetViewModel(new InMemoryTodoService(), TodoWidgetPreferences.Default, () => today);

        viewModel.OpenPopoverForDate(selectedDate);
        Assert.Equal(selectedDate, viewModel.ActivePopoverDate);

        viewModel.ClearHoverDate(selectedDate);

        Assert.Equal(today, viewModel.ActivePopoverDate);
    }

    [Fact]
    public void PinnedPopoverTargetDate_RemainsSelectedAfterHoverLeaves()
    {
        var today = new DateOnly(2026, 7, 13);
        var selectedDate = today.AddDays(2);
        var viewModel = new TodoWidgetViewModel(new InMemoryTodoService(), TodoWidgetPreferences.Default, () => today);

        viewModel.PinDate(selectedDate);
        viewModel.ClearHoverDate(selectedDate);

        Assert.Equal(selectedDate, viewModel.ActivePopoverDate);
    }

    private sealed class InMemoryTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos = new();

        public (DateOnly From, DateOnly To) LastRange { get; private set; }

        public List<TodoDraft> CreatedDrafts { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TodoItem>> GetTodosAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
        {
            LastRange = (fromDate, toDate);
            return Task.FromResult<IReadOnlyList<TodoItem>>(
                _todos.Where(todo => todo.Date >= fromDate && todo.Date <= toDate).ToList());
        }

        public Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
        {
            CreatedDrafts.Add(draft);
            var todo = new TodoItem
            {
                Id = $"todo-{_todos.Count + 1}",
                Date = draft.Date,
                Text = draft.Text,
                StartTime = draft.StartTime,
                EndTime = draft.EndTime,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _todos.Add(todo);
            return Task.FromResult(todo);
        }

        public Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
        {
            todo.Date = draft.Date;
            todo.Text = draft.Text;
            todo.StartTime = draft.StartTime;
            todo.EndTime = draft.EndTime;
            return Task.CompletedTask;
        }

        public Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default)
        {
            todo.IsCompleted = isCompleted;
            return Task.CompletedTask;
        }

        public Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            _todos.Remove(todo);
            return Task.CompletedTask;
        }

        public IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos) => todos.ToList();
    }
}
