using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.TodoWidgets;

public sealed class TodoWidgetDesktopLayerRemovalTests
{
    [Fact]
    public void TodoWidgetWindow_UsesBorderlessBottomWindowInsteadOfDesktopLayer()
    {
        var xaml = XDocument.Load(FindSourcePath("TodoWidgets", "TodoWidgetWindow.xaml"));
        var source = File.ReadAllText(FindSourcePath("TodoWidgets", "TodoWidgetWindow.xaml.cs"));

        Assert.Equal("None", (string?)xaml.Root?.Attribute("WindowStyle"));
        Assert.Equal("False", (string?)xaml.Root?.Attribute("ShowActivated"));
        Assert.DoesNotContain("TryAttachToDesktop", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DesktopLayerService", source, StringComparison.Ordinal);
        Assert.Contains("MoveBehindOtherWindows", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AppStartup_UsesBottomZOrderServiceForTodoWidget()
    {
        var source = File.ReadAllText(FindSourcePath("App.xaml.cs"));

        Assert.DoesNotContain("new DesktopLayerService()", source, StringComparison.Ordinal);
        Assert.Contains("new WindowZOrderService()", source, StringComparison.Ordinal);
    }

    private static string FindSourcePath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName, "src", "QingJian.App" }.Concat(pathParts).ToArray());

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(pathParts)} from test output directory.");
    }
}
