using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class ExportTemporaryStorageTests
{
    [Fact]
    public void CleanupExpiredRemovesOnlyExpiredTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wt-blazor-export-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var expired = Path.Combine(directory, "expired.tmp");
            var current = Path.Combine(directory, "current.tmp");
            var unrelated = Path.Combine(directory, "keep.txt");
            File.WriteAllText(expired, "expired");
            File.WriteAllText(current, "current");
            File.WriteAllText(unrelated, "keep");
            File.SetLastWriteTimeUtc(expired, new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(current, new DateTime(2026, 9, 27, 11, 0, 0, DateTimeKind.Utc));

            var result = ExportTemporaryStorage.CleanupExpired(
                new ExportOptions { TemporaryPath = directory, TemporaryFileRetentionHours = 24 },
                new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc));

            Assert.Equal(new ExportTemporaryCleanupResult(1, 7), result);
            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(current));
            Assert.True(File.Exists(unrelated));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}