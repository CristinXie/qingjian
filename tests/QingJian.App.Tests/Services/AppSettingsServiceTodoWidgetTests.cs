using System.Text.Json;
using QingJian.App.Services;
using QingJian.App.TodoWidgets;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class AppSettingsServiceTodoWidgetTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultTodoWidgetPreferences_WhenSettingsFileDoesNotExist()
    {
        var folder = CreateTempFolder();
        var service = new AppSettingsService(folder);

        var settings = await service.LoadAsync();

        Assert.Equal(TodoWidgetPreferences.Default, settings.TodoWidget);
    }

    [Fact]
    public async Task LoadAsync_LoadsOldEditorOnlySettingsWithTodoDefaults()
    {
        var folder = CreateTempFolder();
        await File.WriteAllTextAsync(
            Path.Combine(folder, "settings.json"),
            """{"editorMode":"markdown"}""");
        var service = new AppSettingsService(folder);

        var settings = await service.LoadAsync();

        Assert.Equal(AppSettings.MarkdownEditorMode, settings.EditorMode);
        Assert.Equal(TodoWidgetPreferences.Default, settings.TodoWidget);
    }

    [Fact]
    public async Task SaveAsync_PersistsTodoWidgetPreferences()
    {
        var folder = CreateTempFolder();
        var service = new AppSettingsService(folder);
        var preferences = TodoWidgetPreferences.Default with
        {
            IsVisible = false,
            Mode = TodoWidgetMode.Calendar,
            Left = 120,
            Top = 80,
            IsLocked = true,
            Opacity = 0.62,
            CalendarYear = 2026,
            CalendarMonth = 8
        };

        await service.SaveAsync(new AppSettings(AppSettings.MarkdownEditorMode, preferences));
        var loaded = await service.LoadAsync();

        Assert.Equal(preferences, loaded.TodoWidget);
    }

    [Fact]
    public async Task SaveEditorModeAsync_PreservesExistingTodoWidgetPreferences()
    {
        var folder = CreateTempFolder();
        var service = new AppSettingsService(folder);
        var preferences = TodoWidgetPreferences.Default with
        {
            IsVisible = false,
            Mode = TodoWidgetMode.Calendar,
            Left = 210,
            Top = 160,
            IsLocked = true,
            Opacity = 0.58,
            CalendarYear = 2027,
            CalendarMonth = 11
        };
        await service.SaveAsync(new AppSettings(AppSettings.DefaultEditorMode, preferences));

        await service.SaveEditorModeAsync(AppSettings.MarkdownEditorMode);
        var loaded = await service.LoadAsync();

        Assert.Equal(AppSettings.MarkdownEditorMode, loaded.EditorMode);
        Assert.Equal(preferences, loaded.TodoWidget);
    }

    [Fact]
    public async Task LoadAsync_NormalizesInvalidTodoWidgetPreferences()
    {
        var folder = CreateTempFolder();
        await File.WriteAllTextAsync(
            Path.Combine(folder, "settings.json"),
            JsonSerializer.Serialize(new
            {
                editorMode = "wysiwyg",
                todoWidget = new
                {
                    isVisible = true,
                    mode = (TodoWidgetMode)99,
                    left = -99999.0,
                    top = -99999.0,
                    opacity = 2.0,
                    isLocked = false,
                    calendarYear = 2026,
                    calendarMonth = 15
                }
            }));
        var service = new AppSettingsService(folder);

        var settings = await service.LoadAsync();

        Assert.Equal(TodoWidgetMode.EightDay, settings.TodoWidget.Mode);
        Assert.Equal(0.75, settings.TodoWidget.Opacity);
        Assert.Equal(DateTime.Today.Year, settings.TodoWidget.CalendarYear);
        Assert.Equal(DateTime.Today.Month, settings.TodoWidget.CalendarMonth);
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "qingjian-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
