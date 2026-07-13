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
        Assert.Contains("new MainWindow(viewModel, settingsService, attachmentService, _todoWidgetCoordinator)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void App_ShutsDownWhenMainWindowCloses()
    {
        var appXaml = XDocument.Load(FindAppXamlPath());

        Assert.Equal("OnMainWindowClose", (string?)appXaml.Root?.Attribute("ShutdownMode"));
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
