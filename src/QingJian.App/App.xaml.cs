using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QingJian.App.Data;
using QingJian.App.Hotkeys;
using QingJian.App.Lifecycle;
using QingJian.App.QuickNotes;
using QingJian.App.Services;
using QingJian.App.Settings;
using QingJian.App.TodoWidgets;
using QingJian.App.Tray;
using QingJian.App.ViewModels;
using QingJian.App.Views;

namespace QingJian.App;

public partial class App : Application
{
    private GlobalHotkeyService? _hotkeyService;
    private TodoWidgetCoordinator? _todoWidgetCoordinator;
    private SettingsWindow? _settingsWindow;
    private FolderManagementWindow? _folderManagementWindow;
    private RecycleBinWindow? _recycleBinWindow;
    private ITrayIconService? _trayIconService;
    private SingleInstanceCoordinator? _singleInstanceCoordinator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceCoordinator = SingleInstanceCoordinator.Create(
            "QingJian.6DCE9EF8-0E5B-4705-86CB-4D397226C321");
        if (!_singleInstanceCoordinator.IsPrimary)
        {
            _singleInstanceCoordinator.NotifyPrimary();
            Shutdown();
            return;
        }

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingJian");
        Directory.CreateDirectory(appDataFolder);
        var webView2UserDataFolder = Path.Combine(appDataFolder, "WebView2");

        var databasePath = Path.Combine(appDataFolder, "qingjian.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var dbContext = new AppDbContext(options);
        var noteRepository = new NoteRepository(dbContext);
        var folderRepository = new FolderRepository(dbContext);
        var folderService = new FolderService(folderRepository);
        var noteService = new NoteService(noteRepository, folderService);
        var todoRepository = new TodoRepository(dbContext);
        var todoService = new TodoService(todoRepository);
        var viewModel = new MainViewModel(noteService, folderService);
        var settingsService = new AppSettingsService(appDataFolder);
        var initialSettings = await settingsService.LoadAsync();
        var windowBehaviorCoordinator = new WindowBehaviorCoordinator(initialSettings.WindowBehavior);
        var attachmentService = new AttachmentService(Path.Combine(appDataFolder, "attachments"));
        _todoWidgetCoordinator = new TodoWidgetCoordinator(
            todoService,
            settingsService,
            new WindowZOrderService());

        var window = new MainWindow(
            viewModel,
            settingsService,
            attachmentService,
            _todoWidgetCoordinator,
            folderService,
            windowBehaviorCoordinator,
            webView2UserDataFolder);
        MainWindow = window;
        _singleInstanceCoordinator.StartListening(
            () => Dispatcher.BeginInvoke(new Action(window.ShowFromTray)));
        var coordinator = new QuickNoteCoordinator(
            noteService,
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
            window.ApplyEditorModePreferenceAsync,
            preferences =>
            {
                window.ApplyWindowBehaviorPreferences(preferences);
                return Task.CompletedTask;
            });

        window.SettingsRequested += (_, _) => ShowSettingsWindow(window, settingsCoordinator);
        window.FolderManagementRequested += async (_, _) =>
            await ShowFolderManagementWindowAsync(window, viewModel, folderService);
        window.RecycleBinRequested += () =>
            ShowRecycleBinWindowAsync(window, viewModel, noteService);
        window.ApplicationExitRequested += (_, _) => Shutdown();

        try
        {
            _trayIconService = new WindowsTrayIconService(executablePath);
            _trayIconService.OpenMainWindowRequested += (_, _) => window.ShowFromTray();
            _trayIconService.QuickNoteRequested += (_, _) => coordinator.OpenQuickNote();
            _trayIconService.ToggleTodoRequested += async (_, _) =>
            {
                try
                {
                    await _todoWidgetCoordinator.ToggleWidgetVisibilityAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"切换桌面待办失败。\n\n{ex.Message}",
                        "QingJian",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
            _trayIconService.ExitRequested += (_, _) => window.RequestApplicationExit();
            _todoWidgetCoordinator.VisibilityChanged += (_, isVisible) =>
                _trayIconService?.SetTodoVisible(isVisible);
            _trayIconService.SetTodoVisible(_todoWidgetCoordinator.IsVisible);
            _trayIconService.Show();
            window.SetTrayAvailable(_trayIconService.IsAvailable);
        }
        catch (Exception ex)
        {
            _trayIconService?.Dispose();
            _trayIconService = null;
            window.SetTrayAvailable(false);
            MessageBox.Show(
                $"系统托盘初始化失败，将使用普通窗口行为。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var shouldStartHidden = windowBehaviorCoordinator.ShouldStartHidden(e.Args, _trayIconService?.IsAvailable == true);

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

            if (shouldStartHidden)
            {
                window.Dispatcher.BeginInvoke(() => window.StartHiddenInTray());
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

    private async Task ShowFolderManagementWindowAsync(
        MainWindow owner,
        MainViewModel mainViewModel,
        IFolderService folderService)
    {
        if (_folderManagementWindow is { IsVisible: true })
        {
            _folderManagementWindow.Activate();
            return;
        }

        try
        {
            var folderViewModel = new FolderManagementViewModel(folderService);
            folderViewModel.FolderRenamed += mainViewModel.ApplyFolderRename;
            folderViewModel.FolderDeleted += mainViewModel.ApplyFolderDeletion;
            _folderManagementWindow = new FolderManagementWindow(folderViewModel)
            {
                Owner = owner
            };

            var selected = _folderManagementWindow.ShowDialog() == true;
            await mainViewModel.RefreshFoldersAsync();
            if (!selected)
            {
                return;
            }

            await owner.ApplyFolderFilterAsync(
                _folderManagementWindow.SelectedAllNotes
                    ? null
                    : _folderManagementWindow.SelectedFolderName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                owner,
                $"打开文件夹管理失败。\n\n{ex.Message}",
                "QingJian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _folderManagementWindow = null;
        }
    }

    private async Task ShowRecycleBinWindowAsync(
        MainWindow owner,
        MainViewModel mainViewModel,
        INoteService noteService)
    {
        if (_recycleBinWindow is { IsVisible: true })
        {
            _recycleBinWindow.Activate();
            return;
        }

        try
        {
            var recycleBinViewModel = new RecycleBinViewModel(noteService);
            _recycleBinWindow = new RecycleBinWindow(recycleBinViewModel)
            {
                Owner = owner
            };
            _recycleBinWindow.ShowDialog();

            await mainViewModel.ReloadActiveNotesAsync();
            await mainViewModel.RefreshFoldersAsync();
        }
        finally
        {
            _recycleBinWindow = null;
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
        _trayIconService?.Dispose();
        _singleInstanceCoordinator?.Dispose();
        base.OnExit(e);
    }
}
