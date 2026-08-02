using System.Security.Cryptography;
using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests;

public sealed class ApplicationIconResourceTests
{
    private const string IconProjectPath = @"Assets\qingjian.ico";
    private const string IconPackUri = "/QingJian.App;component/Assets/qingjian.ico";
    private const string ApprovedIconHash = "1348A3FD637AFBD61470B38B72A873127BBD93AB8EE608FDB6BED4CBCEC6D51D";

    [Fact]
    public void Project_embeds_the_approved_icon_and_uses_it_for_the_executable()
    {
        var projectDirectory = FindProjectDirectory();
        var project = XDocument.Load(Path.Combine(projectDirectory, "QingJian.App.csproj"));

        Assert.Equal(
            IconProjectPath,
            project.Descendants().Single(element => element.Name.LocalName == "ApplicationIcon").Value);
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "Resource"),
            resource => (string?)resource.Attribute("Include") == IconProjectPath);

        var iconPath = Path.Combine(projectDirectory, "Assets", "qingjian.ico");
        Assert.True(File.Exists(iconPath), $"Application icon was not found at {iconPath}.");

        using var icon = File.OpenRead(iconPath);
        Assert.Equal(ApprovedIconHash, Convert.ToHexString(SHA256.HashData(icon)));
    }

    [Fact]
    public void Every_window_uses_the_application_icon()
    {
        var projectDirectory = FindProjectDirectory();
        var windows = Directory
            .EnumerateFiles(projectDirectory, "*.xaml", SearchOption.AllDirectories)
            .Select(path => (Path: path, Document: XDocument.Load(path)))
            .Where(item => item.Document.Root?.Name.LocalName == "Window")
            .ToList();

        var relativePaths = windows
            .Select(item => Path.GetRelativePath(projectDirectory, item.Path).Replace('\\', '/'))
            .OrderBy(path => path)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "QuickNotes/QuickNoteWindow.xaml",
                "Settings/SettingsWindow.xaml",
                "TodoWidgets/TodoWidgetWindow.xaml",
                "Views/FolderManagementWindow.xaml",
                "Views/FolderNameDialog.xaml",
                "Views/MainWindow.xaml",
                "Views/RecycleBinWindow.xaml"
            },
            relativePaths);

        foreach (var window in windows)
        {
            Assert.Equal(IconPackUri, (string?)window.Document.Root!.Attribute("Icon"));
        }
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App");
            if (File.Exists(Path.Combine(candidate, "QingJian.App.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the QingJian.App project directory.");
    }
}
