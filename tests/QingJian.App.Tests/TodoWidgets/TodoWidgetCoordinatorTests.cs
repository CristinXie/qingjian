using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetCoordinatorTests
{
    [Fact]
    public async Task SavePreferencesAsync_PreservesVisiblePreferenceWhenWindowClosesDuringShutdown()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        await settingsService.SaveAsync(new AppSettings(
            AppSettings.DefaultEditorMode,
            TodoWidgetPreferences.Default with { IsVisible = true }));
        FakeTodoWidgetWindow? window = null;
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) =>
            {
                window = new FakeTodoWidgetWindow();
                return window;
            });

        await coordinator.InitializeAsync();
        Assert.NotNull(window);
        Assert.True(window.IsVisible);

        window.SimulateShutdownClose();
        await coordinator.SavePreferencesAsync();
        var settings = await settingsService.LoadAsync();

        Assert.True(settings.TodoWidget.IsVisible);
    }

    [Fact]
    public async Task InitializeAsync_KeepsWidgetHiddenWhenPreviousSessionWasHidden()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        await settingsService.SaveAsync(new AppSettings(
            AppSettings.DefaultEditorMode,
            TodoWidgetPreferences.Default with { IsVisible = false }));
        var windowCreated = false;
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) =>
            {
                windowCreated = true;
                return new FakeTodoWidgetWindow();
            });

        await coordinator.InitializeAsync();
        await coordinator.SavePreferencesAsync();
        var settings = await settingsService.LoadAsync();

        Assert.False(windowCreated);
        Assert.False(coordinator.IsVisible);
        Assert.False(settings.TodoWidget.IsVisible);
    }

    [Fact]
    public async Task HideWidgetAsync_PersistsHiddenPreference()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) => new FakeTodoWidgetWindow());

        await coordinator.InitializeAsync();
        await coordinator.HideWidgetAsync();
        var settings = await settingsService.LoadAsync();

        Assert.False(coordinator.IsVisible);
        Assert.False(settings.TodoWidget.IsVisible);
    }

    [Fact]
    public async Task ToggleWidgetVisibilityAsync_LoadsTodosAndPersistsVisibleWhenShowingHiddenWidget()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        await settingsService.SaveAsync(new AppSettings(
            AppSettings.MarkdownEditorMode,
            TodoWidgetPreferences.Default with { IsVisible = false }));
        var todoService = new RecordingTodoService();
        FakeTodoWidgetWindow? window = null;
        var coordinator = new TodoWidgetCoordinator(
            todoService,
            settingsService,
            (viewModel, _) =>
            {
                window = new FakeTodoWidgetWindow();
                return window;
            });

        await coordinator.InitializeAsync();
        await coordinator.ToggleWidgetVisibilityAsync();
        var settings = await settingsService.LoadAsync();

        Assert.NotNull(window);
        Assert.True(window.IsVisible);
        Assert.Equal(1, window.MoveBehindOtherWindowsCalls);
        Assert.Equal(1, todoService.InitializeCalls);
        Assert.Equal(1, todoService.GetTodosCalls);
        Assert.True(settings.TodoWidget.IsVisible);
        Assert.Equal(AppSettings.MarkdownEditorMode, settings.EditorMode);
    }

    [Fact]
    public async Task VisibilityChanged_ReportsShowAndHideTransitions()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        await settingsService.SaveAsync(new AppSettings(
            AppSettings.DefaultEditorMode,
            TodoWidgetPreferences.Default with { IsVisible = false }));
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) => new FakeTodoWidgetWindow());
        var changes = new List<bool>();
        coordinator.VisibilityChanged += (_, isVisible) => changes.Add(isVisible);

        await coordinator.InitializeAsync();
        await coordinator.ShowWidgetAsync();
        await coordinator.HideWidgetAsync();

        Assert.Equal(new[] { true, false }, changes);
    }

    [Fact]
    public async Task InitializeAsync_ReportsVisibleOnlyAfterRestoredWindowIsShown()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        await settingsService.SaveAsync(new AppSettings(
            AppSettings.DefaultEditorMode,
            TodoWidgetPreferences.Default with { IsVisible = true }));
        FakeTodoWidgetWindow? window = null;
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) => window = new FakeTodoWidgetWindow());
        var windowWasVisibleWhenReported = false;
        coordinator.VisibilityChanged += (_, isVisible) =>
            windowWasVisibleWhenReported = isVisible && window?.IsVisible == true;

        await coordinator.InitializeAsync();

        Assert.True(windowWasVisibleWhenReported);
    }

    [Fact]
    public async Task CapturePreferences_UsesLatestWindowPosition()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        FakeTodoWidgetWindow? window = null;
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) => window = new FakeTodoWidgetWindow());
        await coordinator.InitializeAsync();
        window!.Left = 240;
        window.Top = 170;

        var preferences = coordinator.CapturePreferences();

        Assert.Equal(240, preferences.Left);
        Assert.Equal(170, preferences.Top);
    }

    [Fact]
    public async Task ApplyRuntimePreferencesAsync_UpdatesWindowWithoutPersistingAgain()
    {
        var folder = CreateTempFolder();
        var settingsService = new AppSettingsService(folder);
        FakeTodoWidgetWindow? window = null;
        var coordinator = new TodoWidgetCoordinator(
            new RecordingTodoService(),
            settingsService,
            (viewModel, _) => window = new FakeTodoWidgetWindow());
        await coordinator.InitializeAsync();
        var persistedBeforeApply = await settingsService.LoadAsync();
        var preferences = coordinator.CapturePreferences() with
        {
            IsVisible = false,
            Mode = TodoWidgetMode.Today,
            Left = 120,
            Top = 130,
            Opacity = 0.52,
            IsLocked = true
        };

        await coordinator.ApplyRuntimePreferencesAsync(preferences);
        var runtime = coordinator.CapturePreferences();
        var persistedAfterApply = await settingsService.LoadAsync();

        Assert.False(window!.IsVisible);
        Assert.False(coordinator.IsVisible);
        Assert.Equal(120, window.Left);
        Assert.Equal(130, window.Top);
        Assert.Equal(TodoWidgetMode.Today, runtime.Mode);
        Assert.Equal(0.52, runtime.Opacity);
        Assert.True(runtime.IsLocked);
        Assert.Equal(persistedBeforeApply.TodoWidget, persistedAfterApply.TodoWidget);
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "qingjian-coordinator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed class FakeTodoWidgetWindow : ITodoWidgetWindow
    {
        public bool IsVisible { get; private set; }

        public double Left { get; set; } = 80;

        public double Top { get; set; } = 80;

        public void Show()
        {
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void SimulateShutdownClose()
        {
            IsVisible = false;
        }

        public int MoveBehindOtherWindowsCalls { get; private set; }

        public void MoveBehindOtherWindows()
        {
            MoveBehindOtherWindowsCalls++;
        }
    }

    private sealed class RecordingTodoService : ITodoService
    {
        public int InitializeCalls { get; private set; }

        public int GetTodosCalls { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TodoItem>> GetTodosAsync(
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            GetTodosCalls++;
            return Task.FromResult<IReadOnlyList<TodoItem>>(Array.Empty<TodoItem>());
        }

        public Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos)
        {
            return todos.ToList();
        }
    }
}
