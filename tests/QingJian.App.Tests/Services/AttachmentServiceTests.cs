using QingJian.App.Services;
using Xunit;

namespace QingJian.App.Tests.Services;

public sealed class AttachmentServiceTests : IDisposable
{
    private readonly string _attachmentFolder;

    public AttachmentServiceTests()
    {
        _attachmentFolder = Path.Combine(Path.GetTempPath(), $"qingjian-attachments-{Guid.NewGuid():N}");
    }

    [Fact]
    public void Constructor_CreatesAttachmentFolderForWebViewMapping()
    {
        _ = new AttachmentService(_attachmentFolder);

        Assert.True(Directory.Exists(_attachmentFolder));
    }

    [Fact]
    public async Task SaveImageAsync_WritesDataUrlAndReturnsAssetUrl()
    {
        var service = new AttachmentService(_attachmentFolder);

        var result = await service.SaveImageAsync("照片.png", "data:image/png;base64,AQIDBA==");

        Assert.StartsWith("https://qingjian-assets.local/", result.AssetUrl);
        Assert.EndsWith(".png", result.FileName);
        Assert.True(File.Exists(Path.Combine(_attachmentFolder, result.FileName)));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(_attachmentFolder, result.FileName)));
    }

    [Fact]
    public async Task SaveImageFileAsync_CopiesLocalImageFile()
    {
        Directory.CreateDirectory(_attachmentFolder);
        var sourcePath = Path.Combine(_attachmentFolder, "source.png");
        await File.WriteAllBytesAsync(sourcePath, new byte[] { 5, 6, 7 });
        var service = new AttachmentService(_attachmentFolder);

        var result = await service.SaveImageFileAsync(sourcePath);

        File.Delete(sourcePath);
        Assert.StartsWith("https://qingjian-assets.local/", result.AssetUrl);
        Assert.EndsWith(".png", result.FileName);
        Assert.Equal(new byte[] { 5, 6, 7 }, await File.ReadAllBytesAsync(Path.Combine(_attachmentFolder, result.FileName)));
    }

    [Fact]
    public async Task SaveImageBytesAsync_WritesClipboardImageBytes()
    {
        var service = new AttachmentService(_attachmentFolder);

        var result = await service.SaveImageBytesAsync("clipboard.png", new byte[] { 8, 9, 10 });

        Assert.StartsWith("https://qingjian-assets.local/", result.AssetUrl);
        Assert.EndsWith(".png", result.FileName);
        Assert.Equal(new byte[] { 8, 9, 10 }, await File.ReadAllBytesAsync(Path.Combine(_attachmentFolder, result.FileName)));
    }

    [Theory]
    [InlineData("data:text/plain;base64,AQID")]
    [InlineData("not-a-data-url")]
    [InlineData("data:image/png;base64,%%%")]
    public async Task SaveImageAsync_RejectsInvalidImageData(string dataUrl)
    {
        var service = new AttachmentService(_attachmentFolder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveImageAsync("bad.png", dataUrl));
    }

    [Fact]
    public async Task SaveImageFileAsync_RejectsUnsupportedFileType()
    {
        Directory.CreateDirectory(_attachmentFolder);
        var sourcePath = Path.Combine(_attachmentFolder, "note.txt");
        await File.WriteAllTextAsync(sourcePath, "not an image");
        var service = new AttachmentService(_attachmentFolder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveImageFileAsync(sourcePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_attachmentFolder))
        {
            Directory.Delete(_attachmentFolder, recursive: true);
        }
    }
}
