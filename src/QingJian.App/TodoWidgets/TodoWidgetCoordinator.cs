using System.Windows;
using QingJian.App.Services;

namespace QingJian.App.TodoWidgets;

public sealed class TodoWidgetCoordinator
{
    private readonly ITodoService _todoService;
    private readonly AppSettingsService _settingsService;
    private readonly Func<TodoWidgetViewModel, TodoWidgetCoordinator, ITodoWidgetWindow> _windowFactory;
    private ITodoWidgetWindow? _window;
    private TodoWidgetViewModel? _viewModel;
    private AppSettings _settings = AppSettings.Default;

    public TodoWidgetCoordinator(
        ITodoService todoService,
        AppSettingsService settingsService,
        DesktopLayerService desktopLayerService)
        : this(
            todoService,
            settingsService,
            desktopLayerService,
            (viewModel, coordinator) => new TodoWidgetWindow(viewModel, desktopLayerService, coordinator))
    {
    }

    public TodoWidgetCoordinator(
        ITodoService todoService,
        AppSettingsService settingsService,
        DesktopLayerService desktopLayerService,
        Func<TodoWidgetViewModel, TodoWidgetCoordinator, ITodoWidgetWindow> windowFactory)
    {
        _todoService = todoService;
        _settingsService = settingsService;
        _windowFactory = windowFactory;
    }

    public bool IsVisible => _window?.IsVisible == true;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        _viewModel = new TodoWidgetViewModel(_todoService, _settings.TodoWidget);

        if (_settings.TodoWidget.IsVisible)
        {
            await ShowWidgetAsync(cancellationToken);
        }
    }

    public async Task ShowWidgetAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindow();
        _window!.Show();
        await _viewModel!.LoadAsync(cancellationToken);
        await SavePreferencesAsync(cancellationToken);
    }

    public async Task HideWidgetAsync(CancellationToken cancellationToken = default)
    {
        if (_window is null)
        {
            return;
        }

        _window.Hide();
        await SavePreferencesAsync(cancellationToken);
    }

    public async Task ToggleWidgetVisibilityAsync(CancellationToken cancellationToken = default)
    {
        if (IsVisible)
        {
            await HideWidgetAsync(cancellationToken);
            return;
        }

        await ShowWidgetAsync(cancellationToken);
    }

    public async Task SavePreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (_viewModel is null)
        {
            return;
        }

        var left = _window?.Left ?? _settings.TodoWidget.Left;
        var top = _window?.Top ?? _settings.TodoWidget.Top;
        var preferences = _viewModel.ToPreferences(left, top, IsVisible);
        _settings = await _settingsService
            .SaveTodoWidgetPreferencesAsync(preferences, cancellationToken)
            .ConfigureAwait(false);
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _viewModel ??= new TodoWidgetViewModel(_todoService, _settings.TodoWidget);
        _window = _windowFactory(_viewModel, this);
        _window.Left = _settings.TodoWidget.Left;
        _window.Top = _settings.TodoWidget.Top;
    }
}

public interface ITodoWidgetWindow
{
    bool IsVisible { get; }

    double Left { get; set; }

    double Top { get; set; }

    void Show();

    void Hide();
}
