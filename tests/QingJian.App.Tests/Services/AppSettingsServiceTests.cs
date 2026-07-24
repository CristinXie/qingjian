using QingJian.App.Services;
using QingJian.App.Hotkeys;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _settingsFolder;

    public AppSettingsServiceTests()
    {
        _settingsFolder = Path.Combine(Path.GetTempPath(), $"qingjian-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_settingsFolder);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultSettings_WhenFileDoesNotExist()
    {
        var service = new AppSettingsService(_settingsFolder);

        var settings = await service.LoadAsync();

        Assert.Equal(AppSettings.DefaultEditorMode, settings.EditorMode);
    }

    [Fact]
    public async Task SaveAsync_PersistsEditorMode()
    {
        var service = new AppSettingsService(_settingsFolder);

        await service.SaveAsync(new AppSettings("markdown"));
        var loaded = await service.LoadAsync();

        Assert.Equal("markdown", loaded.EditorMode);
    }

    [Fact]
    public async Task LoadAsync_FallsBackToDefault_WhenEditorModeIsUnsupported()
    {
        var settingsPath = Path.Combine(_settingsFolder, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """{"editorMode":"invalid"}""");
        var service = new AppSettingsService(_settingsFolder);

        var settings = await service.LoadAsync();

        Assert.Equal(AppSettings.DefaultEditorMode, settings.EditorMode);
    }

    [Fact]
    public async Task LoadAsync_LoadsOldSettingsWithDefaultQuickNoteHotkey()
    {
        var settingsPath = Path.Combine(_settingsFolder, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """{"editorMode":"markdown"}""");
        var service = new AppSettingsService(_settingsFolder);

        var settings = await service.LoadAsync();

        Assert.Equal(QuickNoteHotkeyPreferences.Default, settings.QuickNoteHotkey);
    }

    [Fact]
    public async Task UpdateAsync_PreservesDistinctConcurrentFieldUpdates()
    {
        var service = new AppSettingsService(_settingsFolder);
        var hotkey = new QuickNoteHotkeyPreferences(true, HotkeyDefinition.ModControl, 0x4A);

        await Task.WhenAll(
            service.UpdateAsync(settings => settings with { EditorMode = AppSettings.MarkdownEditorMode }),
            service.UpdateAsync(settings => settings with { QuickNoteHotkey = hotkey }));
        var loaded = await service.LoadAsync();

        Assert.Equal(AppSettings.MarkdownEditorMode, loaded.EditorMode);
        Assert.Equal(hotkey, loaded.QuickNoteHotkey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsFolder))
        {
            Directory.Delete(_settingsFolder, recursive: true);
        }
    }
}
