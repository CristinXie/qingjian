using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.Installer;

public sealed class InstallerDefinitionTests
{
    [Fact]
    public void ApplicationProjectDeclaresInstallerVersion()
    {
        var project = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "QingJian.App",
            "QingJian.App.csproj"));

        Assert.Equal(
            "0.1.0",
            project.Descendants().Single(element => element.Name.LocalName == "Version").Value);
    }

    [Fact]
    public void InnoDefinitionCreatesPerUserX64InstallerWithShortcutsAndWebView2Bootstrapper()
    {
        var source = ReadRepositoryFile("installer", "QingJian.iss");

        Assert.Contains("AppId={{6DCE9EF8-0E5B-4705-86CB-4D397226C321}", source);
        Assert.Contains("#define AppVersion \"0.1.0\"", source);
        Assert.Contains("AppVersion={#AppVersion}", source);
        Assert.Contains("DefaultDirName={localappdata}\\Programs\\QingJian", source);
        Assert.Contains("PrivilegesRequired=lowest", source);
        Assert.Contains("ArchitecturesAllowed=x64compatible", source);
        Assert.Contains("OutputBaseFilename=QingJian-Setup", source);
        Assert.Contains("SetupIconFile=", source);
        Assert.Contains("UninstallDisplayIcon={app}\\QingJian.exe", source);
        Assert.Contains("Name: \"desktopicon\"", source);
        Assert.Contains("Flags: checkedonce", source);
        Assert.Contains("{autoprograms}\\QingJian", source);
        Assert.Contains("Source: \"{#PublishDir}\\QingJian.App.exe\"", source);
        Assert.Contains("DestName: \"QingJian.exe\"", source);
        Assert.Contains("{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", source);
        Assert.Contains("Check: not IsWebView2RuntimeInstalled", source);
        Assert.Contains("/silent /install", source);
    }

    [Fact]
    public void BuildScriptPublishesSingleFileAndPassesInputsToInnoCompiler()
    {
        var source = ReadRepositoryFile("scripts", "build-installer.ps1");

        Assert.Contains("-r win-x64", source);
        Assert.Contains("--self-contained true", source);
        Assert.Contains("PublishSingleFile=true", source);
        Assert.Contains("IncludeNativeLibrariesForSelfExtract=true", source);
        Assert.Contains("IncludeAllContentForSelfExtract=true", source);
        Assert.Contains("LinkId=2124703", source);
        Assert.Contains("/DPublishDir=", source);
        Assert.Contains("/DWebView2Bootstrapper=", source);
        Assert.Contains("/DOutputDir=", source);
        Assert.Contains("QingJian-Setup.exe", source);
        Assert.Contains("Test-Path -LiteralPath $setupPath", source);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = segments.Aggregate(FindRepositoryRoot(), Path.Combine);
        Assert.True(File.Exists(path), $"Required installer file does not exist: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QingJian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the QingJian repository root.");
    }
}
