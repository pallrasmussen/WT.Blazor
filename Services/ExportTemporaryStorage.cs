using WT.Blazor.Models;

namespace WT.Blazor.Services;

public static class ExportTemporaryStorage
{
    public static ExportTemporaryCleanupResult CleanupExpired(ExportOptions options, DateTime utcNow)
    {
        var cutoff = utcNow.AddHours(-options.TemporaryFileRetentionHours);
        var filesRemoved = 0;
        long bytesRemoved = 0;
        foreach (var path in Directory.EnumerateFiles(options.TemporaryPath, "*.tmp"))
        {
            var file = new FileInfo(path);
            if (file.LastWriteTimeUtc >= cutoff) continue;

            bytesRemoved += file.Length;
            file.Delete();
            filesRemoved++;
        }
        return new ExportTemporaryCleanupResult(filesRemoved, bytesRemoved);
    }

    public static void EnsureAvailable(ExportOptions options)
    {
        Directory.CreateDirectory(options.TemporaryPath);
        var path = Path.Combine(options.TemporaryPath, $"{Guid.NewGuid():N}.tmp");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    public static FileStream Create(ExportOptions options, out string path)
    {
        path = Path.Combine(options.TemporaryPath, $"{Guid.NewGuid():N}.tmp");
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 65_536,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }
}

public record ExportTemporaryCleanupResult(int FilesRemoved, long BytesRemoved);