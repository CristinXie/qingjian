using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class SettingsWindowCodeBehindTests
{
    [Fact]
    public void CodeBehind_WiresDraftCaptureDataActionsAndGuardedSave()
    {
        var source = File.ReadAllText(FindSettingsWindowCodeBehindPath());

        Assert.Contains("HotkeyTextBox_OnPreviewKeyDown", source, StringComparison.Ordinal);
        Assert.Contains("Key.Back", source, StringComparison.Ordinal);
        Assert.Contains("Key.Escape", source, StringComparison.Ordinal);
        Assert.Contains("Keyboard.Modifiers", source, StringComparison.Ordinal);
        Assert.Contains("KeyInterop.VirtualKeyFromKey", source, StringComparison.Ordinal);
        Assert.Contains("new SettingsSaveRequest", source, StringComparison.Ordinal);
        Assert.Contains("_isSaving", source, StringComparison.Ordinal);
        Assert.Contains("SaveErrorTextBlock.Text", source, StringComparison.Ordinal);
        Assert.Contains("DialogResult = true", source, StringComparison.Ordinal);
        Assert.Contains("_coordinator.OpenDataFolder()", source, StringComparison.Ordinal);
        Assert.Contains("FormatBytes", source, StringComparison.Ordinal);
        Assert.Contains("LaunchAtStartupCheckBox_OnChanged", source, StringComparison.Ordinal);
        Assert.Contains("UpdateStartMinimizedEnabledState", source, StringComparison.Ordinal);
        Assert.Contains("state.AppSettings.WindowBehavior", source, StringComparison.Ordinal);
        Assert.Contains("new WindowBehaviorPreferences", source, StringComparison.Ordinal);
    }

    private static string FindSettingsWindowCodeBehindPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Settings",
                "SettingsWindow.xaml.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate SettingsWindow.xaml.cs.");
    }
}
