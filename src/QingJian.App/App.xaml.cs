using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Hotkeys;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;
    private TodoWidgetCoordinator? _todoWidgetCoordinator;

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

        window.SourceInitialized += (_, _) =>
        {
            _hotkeyService = new GlobalHotkeyService();
            _hotkeyService.HotkeyPressed += (_, _) => coordinator.OpenQuickNote();

            if (!_hotkeyService.Register(window, HotkeyDefinition.QuickNoteHotkey))
            {
                MessageBox.Show(
                    window,
                    $"{HotkeyDefinition.QuickNoteHotkey.DisplayText} 快捷键注册失败，可能已被其他应用占用。",
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
