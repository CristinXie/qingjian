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

        Directory.CreateDirectory(_attachmentFolder);

        var extension = GetSafeExtension(originalFileName, match.Groups["type"].Value);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_attachmentFolder, fileName);

        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return new SavedAttachment(fileName, $"https://{AssetHostName}/{Uri.EscapeDataString(fileName)}");
    }

    private static string GetSafeExtension(string originalFileName, string imageType)
    {
        var originalExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (originalExtension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg")
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
}
