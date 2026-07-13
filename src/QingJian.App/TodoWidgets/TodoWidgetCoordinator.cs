using System.Windows;
using QingJian.App.Services;

namespace QingJian.App.TodoWidgets;

public sealed class TodoWidgetCoordinator
{
    private readonly ITodoService _todoService;
    private readonly AppSettingsService _settingsService;
    private readonly DesktopLayerService _desktopLayerService;
    private TodoWidgetWindow? _window;
    private TodoWidgetViewModel? _viewModel;
    private AppSettings _settings = AppSettings.Default;

    public TodoWidgetCoordinator(
        ITodoService todoService,
        AppSettingsService settingsService,
        DesktopLayerService desktopLayerService)
    {
        _todoService = todoService;
        _settingsService = settingsService;
        _desktopLayerService = desktopLayerService;
    }

    public bool IsVisible => _window?.IsVisible == true;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        _viewModel = new TodoWidgetViewModel(_todoService, _settings.TodoWidget);

        if (_settings.TodoWidget.IsVisible)
        {
            ShowWidget();
            await _viewModel.LoadAsync(cancellationToken);
        }
    }

    public void ShowWidget()
    {
        EnsureWindow();
        _window!.Show();
    }

    public void HideWidget()
    {
        if (_window is null)
        {
            return;
        }

        _window.Hide();
        _ = SavePreferencesAsync();
    }

    public void ToggleWidgetVisibility()
    {
        if (IsVisible)
        {
            HideWidget();
            return;
        }

        ShowWidget();
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
        _settings = _settings with { TodoWidget = preferences };
        await _settingsService.SaveAsync(_settings, cancellationToken);
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _viewModel ??= new TodoWidgetViewModel(_todoService, _settings.TodoWidget);
        _window = new TodoWidgetWindow(_viewModel, _desktopLayerService, this)
        {
            Left = _settings.TodoWidget.Left,
            Top = _settings.TodoWidget.Top
        };
    }
}
