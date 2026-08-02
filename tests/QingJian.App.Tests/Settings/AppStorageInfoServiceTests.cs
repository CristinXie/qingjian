using QingJian.App.Settings;
using Xunit;

namespace QingJian.App.Tests.Settings;

public sealed class AppStorageInfoServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "qingjian-storage-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetInfo_CountsDatabaseAndNestedAttachments()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "attachments", "nested"));
        File.WriteAllBytes(Path.Combine(_folder, "qingjian.db"), new byte[7]);
        File.WriteAllBytes(Path.Combine(_folder, "attachments", "one.png"), new byte[11]);
        File.WriteAllBytes(Path.Combine(_folder, "attachments", "nested", "two.png"), new byte[13]);
        var service = new AppStorageInfoService(_folder);

        var info = service.GetInfo();

        Assert.Equal(_folder, info.DataFolder);
        Assert.Equal(7, info.DatabaseBytes);
        Assert.Equal(24, info.AttachmentBytes);
        Assert.Equal(31, info.TotalBytes);
    }

    [Fact]
    public void GetInfo_SkipsFilesWhoseSizeCannotBeRead()
    {
        var attachments = Path.Combine(_folder, "attachments");
        Directory.CreateDirectory(attachments);
        var readable = Path.Combine(attachments, "readable.png");
        var blocked = Path.Combine(attachments, "blocked.png");
        File.WriteAllBytes(readable, new byte[9]);
        File.WriteAllBytes(blocked, new byte[17]);
        var service = new AppStorageInfoService(
            _folder,
            path => path == blocked ? throw new UnauthorizedAccessException() : new FileInfo(path).Length);

        var info = service.GetInfo();

        Assert.Equal(9, info.AttachmentBytes);
    }

    [Fact]
    public void OpenDataFolder_CreatesAndLaunchesFolder()
    {
        string? launchedPath = null;
        var service = new AppStorageInfoService(_folder, folderLauncher: path => launchedPath = path);

        service.OpenDataFolder();

        Assert.True(Directory.Exists(_folder));
        Assert.Equal(_folder, launchedPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
