using System.IO;
using System.Text.RegularExpressions;

namespace QingJian.App.Services;

public sealed record SavedAttachment(string FileName, string AssetUrl);

public sealed class AttachmentService
{
    public const string AssetHostName = "qingjian-assets.local";
    private static readonly Regex DataUrlPattern = new(
        @"^data:(?<mime>image\/(?<type>png|jpeg|jpg|gif|webp|bmp|svg\+xml));base64,(?<data>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _attachmentFolder;

    public AttachmentService(string attachmentFolder)
    {
        _attachmentFolder = attachmentFolder;
        Directory.CreateDirectory(_attachmentFolder);
    }

    public string AttachmentFolder => _attachmentFolder;

    public async Task<SavedAttachment> SaveImageAsync(
        string originalFileName,
        string dataUrl,
        CancellationToken cancellationToken = default)
    {
        var match = DataUrlPattern.Match(dataUrl);
        if (!match.Success)
        {
            throw new InvalidOperationException("Only image data URLs can be saved as attachments.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Image data is not valid base64.", ex);
        }

        var extension = GetSafeExtension(originalFileName, match.Groups["type"].Value);
        return await SaveImageBytesCoreAsync(extension, bytes, cancellationToken);
    }

    public async Task<SavedAttachment> SaveImageFileAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            throw new InvalidOperationException("Image file does not exist.");
        }

        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        if (!IsSafeImageExtension(extension))
        {
            throw new InvalidOperationException("Only image files can be saved as attachments.");
        }

        var bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await SaveImageBytesCoreAsync(extension, bytes, cancellationToken);
    }

    public Task<SavedAttachment> SaveImageBytesAsync(
        string originalFileName,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!IsSafeImageExtension(extension))
        {
            throw new InvalidOperationException("Only image bytes with a supported image file name can be saved as attachments.");
        }

        return SaveImageBytesCoreAsync(extension, bytes, cancellationToken);
    }

    private async Task<SavedAttachment> SaveImageBytesCoreAsync(
        string extension,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Image data cannot be empty.");
        }

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_attachmentFolder, fileName);

        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return new SavedAttachment(fileName, $"https://{AssetHostName}/{Uri.EscapeDataString(fileName)}");
    }

    private static string GetSafeExtension(string originalFileName, string imageType)
    {
        var originalExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (IsSafeImageExtension(originalExtension))
        {
            return originalExtension;
        }

        return imageType.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ".jpg",
            "svg+xml" => ".svg",
            var type => $".{type}"
        };
    }

    private static bool IsSafeImageExtension(string extension)
    {
        return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
    }
}
