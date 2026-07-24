namespace QingJian.App.Settings;

public interface IAppStorageInfoService
{
    AppStorageInfo GetInfo();

    void OpenDataFolder();
}

public sealed record AppStorageInfo(string DataFolder, long DatabaseBytes, long AttachmentBytes)
{
    public long TotalBytes => DatabaseBytes + AttachmentBytes;
}
