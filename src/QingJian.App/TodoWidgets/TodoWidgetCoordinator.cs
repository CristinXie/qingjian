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
    private bool _isWidgetVisible;

    public TodoWidgetCoordinator(
        ITodoService todoService,
        AppSettingsService settingsService,
        WindowZOrderService windowZOrderService)
        : this(
            todoService,
            settingsService,
            (viewModel, coordinator) => new TodoWidgetWindow(viewModel, windowZOrderService, coordinator))
    {
    }

    public TodoWidgetCoordinator(
        ITodoService todoService,
        AppSettingsService settingsService,
        Func<TodoWidgetViewModel, TodoWidgetCoordinator, ITodoWidgetWindow> windowFactory)
    {
        _todoService = todoService;
        _settingsService = settingsService;
        _windowFactory = windowFactory;
    }

    public bool IsVisible => _isWidgetVisible;

    public event EventHandler<bool>? VisibilityChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        _viewModel = new TodoWidgetViewModel(_todoService, _settings.TodoWidget);
        var restoreVisible = _settings.TodoWidget.IsVisible;
        SetVisibility(false);

        if (restoreVisible)
        {
            await ShowWidgetAsync(cancellationToken);
        }
    }

    public async Task ShowWidgetAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindow();
        _window!.Show();
        SetVisibility(true);
        _window.MoveBehindOtherWindows();
        await _viewModel!.LoadAsync(cancellationToken);
        await SavePreferencesAsync(cancellationToken);
    }

    public async Task HideWidgetAsync(CancellationToken cancellationToken = default)
    {
        _window?.Hide();
        SetVisibility(false);
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

    public TodoWidgetPreferences CapturePreferences()
    {
        if (_viewModel is null)
        {
            return _settings.TodoWidget with { IsVisible = IsVisible };
        }

        var left = _window?.Left ?? _settings.TodoWidget.Left;
        var top = _window?.Top ?? _settings.TodoWidget.Top;
        return _viewModel.ToPreferences(left, top, IsVisible);
    }

    public async Task ApplyRuntimePreferencesAsync(
        TodoWidgetPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var normalized = preferences.Normalize();
        _settings = _settings with { TodoWidget = normalized };
        _viewModel ??= new TodoWidgetViewModel(_todoService, normalized);
        _viewModel.ApplyPreferences(normalized);

        if (normalized.IsVisible)
        {
            EnsureWindow();
            _window!.Left = normalized.Left;
            _window.Top = normalized.Top;
            _window.Show();
            SetVisibility(true);
            _window.MoveBehindOtherWindows();
            await _viewModel.LoadAsync(cancellationToken);
            return;
        }

        if (_window is not null)
        {
            _window.Left = normalized.Left;
            _window.Top = normalized.Top;
            _window.Hide();
        }

        SetVisibility(false);
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

    private void SetVisibility(bool isVisible)
    {
        if (_isWidgetVisible == isVisible)
        {
            return;
        }

        _isWidgetVisible = isVisible;
        VisibilityChanged?.Invoke(this, isVisible);
    }
}

public interface ITodoWidgetWindow
{
    bool IsVisible { get; }

    double Left { get; set; }

    double Top { get; set; }

    void Show();

    void Hide();

    void MoveBehindOtherWindows();
}
