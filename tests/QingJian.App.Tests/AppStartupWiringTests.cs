using Xunit;
using System.Xml.Linq;

namespace QingJian.App.Tests;

public sealed class AppStartupWiringTests
{
    [Fact]
    public void Startup_WiresTodoWidgetCoordinator()
    {
        var source = File.ReadAllText(FindAppSourcePath());

        Assert.Contains("var todoRepository = new TodoRepository(dbContext);", source, StringComparison.Ordinal);
        Assert.Contains("var todoService = new TodoService(todoRepository);", source, StringComparison.Ordinal);
        Assert.Contains("_todoWidgetCoordinator = new TodoWidgetCoordinator(", source, StringComparison.Ordinal);
        Assert.Contains("await _todoWidgetCoordinator.InitializeAsync();", source, StringComparison.Ordinal);
        Assert.Contains("var window = new MainWindow(", source, StringComparison.Ordinal);
        Assert.Contains("windowBehaviorCoordinator);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void App_UsesExplicitShutdownForTrayResidency()
    {
        var appXaml = XDocument.Load(FindAppXamlPath());

        Assert.Equal("OnExplicitShutdown", (string?)appXaml.Root?.Attribute("ShutdownMode"));
    }

    [Fact]
    public void Startup_WiresSettingsAndConfigurableHotkeyServices()
    {
        var source = File.ReadAllText(FindAppSourcePath());

        Assert.Contains("await settingsService.LoadAsync()", source, StringComparison.Ordinal);
        Assert.Contains("new WindowsStartupRegistrationService", source, StringComparison.Ordinal);
        Assert.Contains("new AppStorageInfoService(appDataFolder)", source, StringComparison.Ordinal);
        Assert.Contains("new AppRuntimeInfoProvider()", source, StringComparison.Ordinal);
        Assert.Contains("new QuickNoteHotkeyCoordinator", source, StringComparison.Ordinal);
        Assert.Contains("new SettingsCoordinator", source, StringComparison.Ordinal);
        Assert.Contains("_hotkeyService.Attach(window)", source, StringComparison.Ordinal);
        Assert.Contains("initialSettings.QuickNoteHotkey", source, StringComparison.Ordinal);
        Assert.Contains("window.SettingsRequested", source, StringComparison.Ordinal);
        Assert.Contains("_settingsWindow", source, StringComparison.Ordinal);
        Assert.Contains("_settingsWindow.Activate()", source, StringComparison.Ordinal);
        Assert.Contains("new SettingsWindow(settingsCoordinator)", source, StringComparison.Ordinal);
        Assert.Contains("Environment.ProcessPath", source, StringComparison.Ordinal);
        Assert.Contains("new WindowBehaviorCoordinator(initialSettings.WindowBehavior)", source, StringComparison.Ordinal);
        Assert.Contains("new WindowsTrayIconService(executablePath)", source, StringComparison.Ordinal);
        Assert.Contains("OpenMainWindowRequested", source, StringComparison.Ordinal);
        Assert.Contains("QuickNoteRequested", source, StringComparison.Ordinal);
        Assert.Contains("ToggleTodoRequested", source, StringComparison.Ordinal);
        Assert.Contains("ExitRequested", source, StringComparison.Ordinal);
        Assert.Contains("window.ShowFromTray()", source, StringComparison.Ordinal);
        Assert.Contains("coordinator.OpenQuickNote()", source, StringComparison.Ordinal);
        Assert.Contains("ToggleWidgetVisibilityAsync", source, StringComparison.Ordinal);
        Assert.Contains("window.RequestApplicationExit()", source, StringComparison.Ordinal);
        Assert.Contains("SetTodoVisible", source, StringComparison.Ordinal);
        Assert.Contains("ShouldStartHidden(e.Args", source, StringComparison.Ordinal);
        Assert.Contains("window.StartHiddenInTray()", source, StringComparison.Ordinal);
        Assert.Contains("_trayIconService?.Dispose()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_WiresFolderRepositoryServiceAndManagerWindow()
    {
        var source = File.ReadAllText(FindAppSourcePath());

        Assert.Contains("var folderRepository = new FolderRepository(dbContext);", source, StringComparison.Ordinal);
        Assert.Contains("var folderService = new FolderService(folderRepository);", source, StringComparison.Ordinal);
        Assert.Contains("new NoteService(noteRepository, folderService)", source, StringComparison.Ordinal);
        Assert.Contains("new MainViewModel(noteService, folderService)", source, StringComparison.Ordinal);
        Assert.Contains("_folderManagementWindow", source, StringComparison.Ordinal);
        Assert.Contains("new FolderManagementWindow", source, StringComparison.Ordinal);
        Assert.Contains("FolderManagementRequested", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_WiresRecycleBinWindowAndRefreshesMainNavigationAfterClose()
    {
        var source = File.ReadAllText(FindAppSourcePath());

        Assert.Contains("RecycleBinRequested", source, StringComparison.Ordinal);
        Assert.Contains("new RecycleBinViewModel(noteService)", source, StringComparison.Ordinal);
        Assert.Contains("new RecycleBinWindow", source, StringComparison.Ordinal);
        Assert.Contains("await mainViewModel.ReloadActiveNotesAsync()", source, StringComparison.Ordinal);
        Assert.Contains("await mainViewModel.RefreshFoldersAsync()", source, StringComparison.Ordinal);
    }

    private static string FindAppSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "App.xaml.cs");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find App.xaml.cs from test output directory.");
    }

    private static string FindAppXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "App.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find App.xaml from test output directory.");
    }
}
