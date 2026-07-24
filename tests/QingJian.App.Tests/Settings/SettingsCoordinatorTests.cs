using QingJian.App.Hotkeys;
using QingJian.App.Models;
using QingJian.App.Services;
using QingJian.App.Settings;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class SettingsCoordinatorTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "qingjian-settings-coordinator-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_CombinesPersistedRuntimeAndEnvironmentState()
    {
        var context = await CreateContextAsync();
        context.Startup.IsEnabled = true;

        var state = await context.Coordinator.LoadAsync();

        Assert.True(state.LaunchAtStartup);
        Assert.Equal(AppSettings.DefaultEditorMode, state.AppSettings.EditorMode);
        Assert.Equal(context.TodoCoordinator.CapturePreferences(), state.TodoWidget);
        Assert.Equal(31, state.Storage.TotalBytes);
        Assert.Equal("126.0", state.Runtime.WebView2Version);
    }

    [Fact]
    public async Task SaveAsync_RejectsMissingEnabledHotkeyBeforeSideEffects()
    {
        var context = await CreateContextAsync();
        var request = CreateRequest() with
        {
            QuickNoteHotkey = new QuickNoteHotkeyPreferences(true, 0, 0)
        };

        var result = await context.Coordinator.SaveAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("请选择有效的快捷键。", result.ErrorMessage);
        Assert.Equal(0, context.Startup.SetCalls);
        Assert.Equal(QuickNoteHotkeyPreferences.Default, context.HotkeyCoordinator.CurrentPreferences);
    }

    [Fact]
    public async Task SaveAsync_PersistsAndAppliesAllEditableSettings()
    {
        var context = await CreateContextAsync();
        var request = CreateRequest();

        var result = await context.Coordinator.SaveAsync(request);
        var persisted = await context.SettingsService.LoadAsync();
        var runtimeTodo = context.TodoCoordinator.CapturePreferences();

        Assert.True(result.Succeeded);
        Assert.True(context.Startup.IsEnabled);
        Assert.Equal(AppSettings.MarkdownEditorMode, persisted.EditorMode);
        Assert.Equal(request.QuickNoteHotkey, persisted.QuickNoteHotkey);
        Assert.Equal(AppSettings.MarkdownEditorMode, context.LastAppliedEditorMode);
        Assert.Equal(TodoWidgetMode.Today, runtimeTodo.Mode);
        Assert.Equal(0.52, runtimeTodo.Opacity);
        Assert.True(runtimeTodo.IsLocked);
    }

    [Fact]
    public async Task SaveAsync_HotkeyConflictKeepsEveryOtherSettingUnchanged()
    {
        var context = await CreateContextAsync(rejectVirtualKey: 0x4A);

        var result = await context.Coordinator.SaveAsync(CreateRequest());
        var persisted = await context.SettingsService.LoadAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("快捷键已被其他应用占用，请重新设置。", result.ErrorMessage);
        Assert.False(context.Startup.IsEnabled);
        Assert.Equal(AppSettings.DefaultEditorMode, persisted.EditorMode);
        Assert.Equal(QuickNoteHotkeyPreferences.Default, persisted.QuickNoteHotkey);
        Assert.Null(context.LastAppliedEditorMode);
    }

    [Fact]
    public async Task SaveAsync_EditorFailureRollsBackRuntimeAndPersistence()
    {
        var context = await CreateContextAsync(failEditorMode: AppSettings.MarkdownEditorMode);
        var oldTodo = context.TodoCoordinator.CapturePreferences();

        var result = await context.Coordinator.SaveAsync(CreateRequest());
        var persisted = await context.SettingsService.LoadAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("无法应用编辑模式", result.ErrorMessage, StringComparison.Ordinal);
        Assert.False(context.Startup.IsEnabled);
        Assert.Equal(AppSettings.DefaultEditorMode, persisted.EditorMode);
        Assert.Equal(QuickNoteHotkeyPreferences.Default, persisted.QuickNoteHotkey);
        Assert.Equal(QuickNoteHotkeyPreferences.Default, context.HotkeyCoordinator.CurrentPreferences);
        Assert.Equal(oldTodo, context.TodoCoordinator.CapturePreferences());
        Assert.Equal(AppSettings.DefaultEditorMode, context.LastAppliedEditorMode);
    }

    private async Task<TestContext> CreateContextAsync(uint? rejectVirtualKey = null, string? failEditorMode = null)
    {
        Directory.CreateDirectory(_folder);
        var settingsService = new AppSettingsService(_folder);
        await settingsService.SaveAsync(AppSettings.Default with
        {
            TodoWidget = TodoWidgetPreferences.Default with { IsVisible = false }
        });
        var registrar = new FakeRegistrar { RejectVirtualKey = rejectVirtualKey };
        var hotkeyCoordinator = new QuickNoteHotkeyCoordinator(registrar);
        Assert.True(hotkeyCoordinator.Apply(QuickNoteHotkeyPreferences.Default));
        var todoCoordinator = new TodoWidgetCoordinator(
            new EmptyTodoService(),
            settingsService,
            (viewModel, _) => new FakeTodoWindow());
        await todoCoordinator.InitializeAsync();
        var startup = new FakeStartupService();
        var context = new TestContext(settingsService, startup, hotkeyCoordinator, todoCoordinator);
        context.Coordinator = new SettingsCoordinator(
            settingsService,
            startup,
            hotkeyCoordinator,
            todoCoordinator,
            new FakeStorageService(),
            new FakeRuntimeProvider(),
            mode =>
            {
                if (mode == failEditorMode)
                {
                    throw new InvalidOperationException("无法应用编辑模式");
                }

                context.LastAppliedEditorMode = mode;
                return Task.CompletedTask;
            });
        return context;
    }

    private static SettingsSaveRequest CreateRequest()
    {
        return new SettingsSaveRequest(
            LaunchAtStartup: true,
            EditorMode: AppSettings.MarkdownEditorMode,
            QuickNoteHotkey: new QuickNoteHotkeyPreferences(true, HotkeyDefinition.ModControl, 0x4A),
            TodoWidget: new TodoWidgetSettingsSelection(
                IsVisible: false,
                Mode: TodoWidgetMode.Today,
                Opacity: 0.52,
                IsLocked: true,
                ResetPositionAndAppearance: false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class TestContext
    {
        public TestContext(
            AppSettingsService settingsService,
            FakeStartupService startup,
            QuickNoteHotkeyCoordinator hotkeyCoordinator,
            TodoWidgetCoordinator todoCoordinator)
        {
            SettingsService = settingsService;
            Startup = startup;
            HotkeyCoordinator = hotkeyCoordinator;
            TodoCoordinator = todoCoordinator;
        }

        public AppSettingsService SettingsService { get; }

        public FakeStartupService Startup { get; }

        public QuickNoteHotkeyCoordinator HotkeyCoordinator { get; }

        public TodoWidgetCoordinator TodoCoordinator { get; }

        public SettingsCoordinator Coordinator { get; set; } = null!;

        public string? LastAppliedEditorMode { get; set; }
    }

    private sealed class FakeStartupService : IStartupRegistrationService
    {
        public bool IsEnabled { get; set; }

        public int SetCalls { get; private set; }

        public void SetEnabled(bool enabled)
        {
            SetCalls++;
            IsEnabled = enabled;
        }
    }

    private sealed class FakeRegistrar : IGlobalHotkeyRegistrar
    {
        public event EventHandler? HotkeyPressed
        {
            add { }
            remove { }
        }

        public uint? RejectVirtualKey { get; init; }

        public HotkeyDefinition? Registered { get; private set; }

        public bool TryRegister(HotkeyDefinition hotkey)
        {
            if (hotkey.VirtualKey == RejectVirtualKey)
            {
                return false;
            }

            Registered = hotkey;
            return true;
        }

        public void Unregister() => Registered = null;
    }

    private sealed class FakeStorageService : IAppStorageInfoService
    {
        public AppStorageInfo GetInfo() => new(_folderPlaceholder, 7, 24);

        public void OpenDataFolder()
        {
        }

        private const string _folderPlaceholder = @"C:\Data\QingJian";
    }

    private sealed class FakeRuntimeProvider : IAppRuntimeInfoProvider
    {
        public AppRuntimeInfo GetInfo() => new("1.0.0", ".NET 8", "126.0");
    }

    private sealed class FakeTodoWindow : ITodoWidgetWindow
    {
        public bool IsVisible { get; private set; }

        public double Left { get; set; } = 80;

        public double Top { get; set; } = 80;

        public void Show() => IsVisible = true;

        public void Hide() => IsVisible = false;

        public void MoveBehindOtherWindows()
        {
        }
    }

    private sealed class EmptyTodoService : ITodoService
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TodoItem>> GetTodosAsync(
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(Array.Empty<TodoItem>());

        public Task<TodoItem> CreateTodoAsync(TodoDraft draft, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateTodoAsync(TodoItem todo, TodoDraft draft, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetCompletedAsync(TodoItem todo, bool isCompleted, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteTodoAsync(TodoItem todo, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyList<TodoItem> SortTodos(IEnumerable<TodoItem> todos) => todos.ToList();
    }
}
