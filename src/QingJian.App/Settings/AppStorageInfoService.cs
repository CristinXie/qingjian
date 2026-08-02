using System.Diagnostics;
using System.IO;

namespace QingJian.App.Settings;

public sealed class AppStorageInfoService : IAppStorageInfoService
{
    private readonly string _dataFolder;
    private readonly Func<string, long> _fileSizeReader;
    private readonly Action<string> _folderLauncher;

    public AppStorageInfoService(
        string dataFolder,
        Func<string, long>? fileSizeReader = null,
        Action<string>? folderLauncher = null)
    {
        _dataFolder = dataFolder;
        _fileSizeReader = fileSizeReader ?? (path => new FileInfo(path).Length);
        _folderLauncher = folderLauncher ?? LaunchFolder;
    }

    public AppStorageInfo GetInfo()
    {
        Directory.CreateDirectory(_dataFolder);
        var databaseBytes = TryGetFileSize(Path.Combine(_dataFolder, "qingjian.db"));
        var attachmentBytes = GetDirectorySize(Path.Combine(_dataFolder, "attachments"));
        return new AppStorageInfo(_dataFolder, databaseBytes, attachmentBytes);
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(_dataFolder);
        _folderLauncher(_dataFolder);
    }

    private long GetDirectorySize(string root)
    {
        if (!Directory.Exists(root))
        {
            return 0;
        }

        long total = 0;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    total += TryGetFileSize(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    pending.Push(child);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return total;
    }

    private long TryGetFileSize(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            return _fileSizeReader(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static void LaunchFolder(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
