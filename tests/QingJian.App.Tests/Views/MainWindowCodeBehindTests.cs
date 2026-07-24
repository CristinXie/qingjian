using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class MainWindowCodeBehindTests
{
    [Fact]
    public void MainWindow_WiresProtectedEditorWorkflowActions()
    {
        var source = File.ReadAllText(FindMainWindowCodeBehindPath());

        Assert.Contains("DeleteButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("UndoButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("RedoButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("ModeToggleButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("TitleTextBox_OnPreviewKeyDown", source, StringComparison.Ordinal);
        Assert.Contains("window.qingjianEditor.undo()", source, StringComparison.Ordinal);
        Assert.Contains("window.qingjianEditor.redo()", source, StringComparison.Ordinal);
        Assert.Contains("window.qingjianEditor.toggleMode()", source, StringComparison.Ordinal);
        Assert.Contains("window.qingjianEditor.focus()", source, StringComparison.Ordinal);
        Assert.Contains("RefreshNoteNavigation()", source, StringComparison.Ordinal);
        Assert.Contains("关闭前无法保存当前便签", source, StringComparison.Ordinal);
        Assert.Contains("VisibilityChanged", source, StringComparison.Ordinal);
        Assert.Contains("TodoWidgetVisibilityAction.GetLabel", source, StringComparison.Ordinal);
        Assert.Contains("NoteListBox_OnSelectionChanged", source, StringComparison.Ordinal);
        Assert.Contains("FavoriteButton_OnPreviewMouseLeftButtonDown", source, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", source, StringComparison.Ordinal);
        Assert.Contains("ToggleFavoriteAsync", source, StringComparison.Ordinal);
        Assert.Contains("收藏状态保存失败", source, StringComparison.Ordinal);
        Assert.Contains("LoadSelectedNoteIfChangedAsync", source, StringComparison.Ordinal);
        Assert.Contains("_editorState.ShouldReloadSelection", source, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? SettingsRequested", source, StringComparison.Ordinal);
        Assert.Contains("SettingsButton_OnClick", source, StringComparison.Ordinal);
        Assert.Contains("SettingsRequested?.Invoke", source, StringComparison.Ordinal);
        Assert.Contains("public async Task ApplyEditorModePreferenceAsync", source, StringComparison.Ordinal);
        Assert.Contains("await SetEditorModeAsync(_currentEditorMode)", source, StringComparison.Ordinal);
    }

    private static string FindMainWindowCodeBehindPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Views",
                "MainWindow.xaml.cs");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find MainWindow.xaml.cs from test output directory.");
    }
}
