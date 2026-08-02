using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class RecycleBinWindowCodeBehindTests
{
    [Fact]
    public void Window_LoadsAndExecutesDirectLifecycleActionsWithChineseErrors()
    {
        var source = File.ReadAllText(FindPath("RecycleBinWindow.xaml.cs"));

        Assert.Contains("await _viewModel.LoadAsync()", source, StringComparison.Ordinal);
        Assert.Contains("RestoreNoteButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("await _viewModel.RestoreAsync(note)", source, StringComparison.Ordinal);
        Assert.Contains("PermanentlyDeleteNoteButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("await _viewModel.PermanentlyDeleteAsync(note)", source, StringComparison.Ordinal);
        Assert.Contains("恢复便签失败", source, StringComparison.Ordinal);
        Assert.Contains("永久删除便签失败", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBoxButton.YesNo", source, StringComparison.Ordinal);
    }

    private static string FindPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "QingJian.App", "Views", fileName);
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {fileName}.");
    }
}
