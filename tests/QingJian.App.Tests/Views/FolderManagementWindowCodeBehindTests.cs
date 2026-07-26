using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class FolderManagementWindowCodeBehindTests
{
    [Fact]
    public void CodeBehind_UsesSafeSelectionAndDeleteConfirmation()
    {
        var source = File.ReadAllText(FindPath("FolderManagementWindow.xaml.cs"));

        Assert.Contains("MessageBoxButton.YesNo", source, StringComparison.Ordinal);
        Assert.Contains("MessageBoxResult.No", source, StringComparison.Ordinal);
        Assert.Contains("其中 {item.NoteCount} 条便签将移至未分类", source, StringComparison.Ordinal);
        Assert.Contains("确定删除空文件夹", source, StringComparison.Ordinal);
        Assert.Contains("DialogResult = true", source, StringComparison.Ordinal);
        Assert.Contains("SelectedFolderName", source, StringComparison.Ordinal);
        Assert.Contains("SelectedAllNotes", source, StringComparison.Ordinal);
    }

    private static string FindPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "QingJian.App", "Views", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName}.");
    }
}
