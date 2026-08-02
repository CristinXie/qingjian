using Xunit;

namespace QingJian.App.Tests.Tray;

public sealed class MainWindowTrayLifecycleTests
{
    [Fact]
    public void MainWindow_SavesBeforeHideAndRestoresTaskbarState()
    {
        var source = File.ReadAllText(FindMainWindowSourcePath());

        Assert.Contains("await PullLatestEditorMarkdownAsync()", source, StringComparison.Ordinal);
        Assert.Contains("await _viewModel.SaveSelectedNoteNowAsync()", source, StringComparison.Ordinal);
        Assert.Contains("HideToTrayAfterSaveAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShowInTaskbar = false", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShowInTaskbar = true", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WindowState = _restoreWindowState", source, StringComparison.Ordinal);
        Assert.Contains("Hide()", source, StringComparison.Ordinal);
        Assert.Contains("Show()", source, StringComparison.Ordinal);
        Assert.Contains("Activate()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ExplicitExitBypassesCloseToTrayAndGuardsReentrancy()
    {
        var source = File.ReadAllText(FindMainWindowSourcePath());

        Assert.Contains("_isExplicitExitRequested = true", source, StringComparison.Ordinal);
        Assert.Contains("_isWindowTransitionInProgress", source, StringComparison.Ordinal);
        Assert.Contains("ShouldHideOnClose(_isExplicitExitRequested", source, StringComparison.Ordinal);
        Assert.Contains("ApplicationExitRequested?.Invoke", source, StringComparison.Ordinal);
        Assert.Contains("最小化到托盘前无法保存当前便签", source, StringComparison.Ordinal);
        Assert.Contains("关闭前无法保存当前便签", source, StringComparison.Ordinal);
    }

    private static string FindMainWindowSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App", "Views", "MainWindow.xaml.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate MainWindow.xaml.cs.");
    }
}
