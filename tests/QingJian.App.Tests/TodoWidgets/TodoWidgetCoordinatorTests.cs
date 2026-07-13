using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetCoordinatorTests
{
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
            new DesktopLayerService(),
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
        Assert.Equal(1, todoService.InitializeCalls);
        Assert.Equal(1, todoService.GetTodosCalls);
        Assert.True(settings.TodoWidget.IsVisible);
        Assert.Equal(AppSettings.MarkdownEditorMode, settings.EditorMode);
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
