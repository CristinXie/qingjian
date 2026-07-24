using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Hotkeys;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.Settings;
using QingJian.App.TodoWidgets;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;
    private TodoWidgetCoordinator? _todoWidgetCoordinator;
    private SettingsWindow? _settingsWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingJian");
        Directory.CreateDirectory(appDataFolder);

        var databasePath = Path.Combine(appDataFolder, "qingjian.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        var repository = new NoteRepository(dbContext);
        var service = new NoteService(repository);
        var todoRepository = new TodoRepository(dbContext);
        var todoService = new TodoService(todoRepository);
        var viewModel = new MainViewModel(service);
        var settingsService = new AppSettingsService(appDataFolder);
        var initialSettings = await settingsService.LoadAsync();
        var attachmentService = new AttachmentService(Path.Combine(appDataFolder, "attachments"));
        _todoWidgetCoordinator = new TodoWidgetCoordinator(
            todoService,
            settingsService,
            new WindowZOrderService());

        var window = new MainWindow(viewModel, settingsService, attachmentService, _todoWidgetCoordinator);
        MainWindow = window;
        var coordinator = new QuickNoteCoordinator(
            service,
            viewModel,
            new QuickNoteWindowFactory(),
            () => window.IsVisible && window.WindowState != WindowState.Minimized);

        _hotkeyService = new GlobalHotkeyService();
        var hotkeyCoordinator = new QuickNoteHotkeyCoordinator(_hotkeyService);
        _hotkeyService.HotkeyPressed += (_, _) => coordinator.OpenQuickNote();
        var executablePath = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "QingJian.App.exe");
        var startupService = new WindowsStartupRegistrationService(executablePath);
        var storageService = new AppStorageInfoService(appDataFolder);
        var runtimeInfoProvider = new AppRuntimeInfoProvider();
        var settingsCoordinator = new SettingsCoordinator(
            settingsService,
            startupService,
            hotkeyCoordinator,
            _todoWidgetCoordinator,
            storageService,
            runtimeInfoProvider,
            window.ApplyEditorModePreferenceAsync);

        window.SettingsRequested += (_, _) => ShowSettingsWindow(window, settingsCoordinator);

        window.SourceInitialized += (_, _) =>
        {
            var attached = _hotkeyService.Attach(window);
            var applied = hotkeyCoordinator.Apply(initialSettings.QuickNoteHotkey);
            if (!attached || (initialSettings.QuickNoteHotkey.IsEnabled && !applied))
            {
                MessageBox.Show(
                    window,
                    $"{HotkeyGestureFormatter.Format(initialSettings.QuickNoteHotkey.Modifiers, initialSettings.QuickNoteHotkey.VirtualKey)} 快捷键注册失败，可能已被其他应用占用。",
                    "QingJian",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };

        window.Show();
        try
        {
            await _todoWidgetCoordinator.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                window,
                $"桌面待办组件启动失败，便签功能仍可继续使用。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ShowSettingsWindow(MainWindow owner, SettingsCoordinator settingsCoordinator)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(settingsCoordinator)
        {
            Owner = owner
        };

        try
        {
            _settingsWindow.ShowDialog();
        }
        finally
        {
            _settingsWindow = null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _todoWidgetCoordinator?.SavePreferencesAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
        }

        _hotkeyService?.Dispose();
        base.OnExit(e);
    }
}
