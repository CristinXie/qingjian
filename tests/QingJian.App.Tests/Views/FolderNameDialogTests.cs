using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class FolderNameDialogTests
{
    [Fact]
    public void Dialog_UsesChineseNamePromptAndKeyboardActions()
    {
        var xaml = File.ReadAllText(FindPath("FolderNameDialog.xaml"));
        var source = File.ReadAllText(FindPath("FolderNameDialog.xaml.cs"));

        Assert.Contains("新建文件夹", xaml, StringComparison.Ordinal);
        Assert.Contains("FolderNameTextBox", xaml, StringComparison.Ordinal);
        Assert.Contains("ErrorTextBlock", xaml, StringComparison.Ordinal);
        Assert.Contains("Key.Enter", source, StringComparison.Ordinal);
        Assert.Contains("Key.Escape", source, StringComparison.Ordinal);
        Assert.Contains("CreatedFolderName", source, StringComparison.Ordinal);
        Assert.Contains("SubmitAsync", source, StringComparison.Ordinal);
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
