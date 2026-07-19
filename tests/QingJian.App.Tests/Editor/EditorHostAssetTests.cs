using Xunit;

namespace QingJian.App.Tests.Editor;

public sealed class EditorHostAssetTests
{
    [Fact]
    public void EditorHost_ExposesLocalizedHistoryModeAndFocusContracts()
    {
        var script = File.ReadAllText(FindAssetPath("editor-host.js"));
        var css = File.ReadAllText(FindAssetPath("editor-host.css"));

        Assert.Contains("hideModeSwitch: true", script, StringComparison.Ordinal);
        Assert.Contains("language: \"zh-CN\"", script, StringComparison.Ordinal);
        Assert.Contains("undo: function", script, StringComparison.Ordinal);
        Assert.Contains("redo: function", script, StringComparison.Ordinal);
        Assert.Contains("toggleMode: function", script, StringComparison.Ordinal);
        Assert.Contains("focus: function", script, StringComparison.Ordinal);
        Assert.Contains("event.key.toLowerCase() === \"y\"", script, StringComparison.Ordinal);
        Assert.Contains("切换到所见即所得", script, StringComparison.Ordinal);
        Assert.Contains("Markdown", script, StringComparison.Ordinal);
        Assert.Contains(".toastui-editor-mode-switch", css, StringComparison.Ordinal);
        Assert.Contains("display: none", css, StringComparison.Ordinal);
    }

    private static string FindAssetPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "EditorAssets",
                fileName);

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {fileName} from test output directory.");
    }
}
