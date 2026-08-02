using Xunit;

namespace QingJian.App.Tests.Tray;

public sealed class WindowsTrayIconServiceTests
{
    [Fact]
    public void Service_UsesNativeNotifyIconAndCompleteChineseMenu()
    {
        var source = File.ReadAllText(FindSourcePath("WindowsTrayIconService.cs"));

        Assert.Contains("NotifyIcon", source, StringComparison.Ordinal);
        Assert.Contains("ContextMenuStrip", source, StringComparison.Ordinal);
        Assert.Contains("打开主窗口", source, StringComparison.Ordinal);
        Assert.Contains("新建快捷便签", source, StringComparison.Ordinal);
        Assert.Contains("显示桌面待办", source, StringComparison.Ordinal);
        Assert.Contains("隐藏桌面待办", source, StringComparison.Ordinal);
        Assert.Contains("退出 QingJian", source, StringComparison.Ordinal);
        Assert.Contains("DoubleClick", source, StringComparison.Ordinal);
        Assert.Contains("Icon.ExtractAssociatedIcon", source, StringComparison.Ordinal);
        Assert.Contains("SystemIcons.Application", source, StringComparison.Ordinal);
        Assert.Contains("_notifyIcon.Dispose()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_EnablesBuiltInWindowsFormsSupportWithoutTrayPackage()
    {
        var project = File.ReadAllText(FindProjectPath());

        Assert.Contains("<UseWindowsForms>true</UseWindowsForms>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Hardcodet", project, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindSourcePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App", "Tray", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName}.");
    }

    private static string FindProjectPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App", "QingJian.App.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate QingJian.App.csproj.");
    }
}
