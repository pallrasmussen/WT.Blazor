using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class DatabaseMaintenanceTests
{
    [Fact]
    public async Task RecoveryCopiesAndVerifiesDataAndKeysWithoutChangingTheBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wt-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var database = Path.Combine(root, "backup.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={database};Pooling=False").Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.PortalSettings.Add(new PortalSettings());
            var item = new WaitingTime { Name = "Real record", Hospital = Hospital.Klaksvikar };
            db.WaitingTimes.Add(item);
            await db.SaveChangesAsync();
            db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry { WaitingTimeId = item.Id, Name = item.Name, Username = "demo", Detail = "Demo" });
            await db.SaveChangesAsync();
        }
        var keys = Path.Combine(root, "backup.keys");
        Directory.CreateDirectory(keys);
        DataProtectionProvider.Create(new DirectoryInfo(keys), builder => builder.SetApplicationName("WT.Blazor"))
            .CreateProtector("fixture").Protect("original token");
        await File.WriteAllTextAsync(database + ".probe", DatabaseMaintenance.CreateRecoveryProbe(keys));
        var before = SHA256.HashData(await File.ReadAllBytesAsync(database));

        var report = await DatabaseMaintenance.VerifyBackupAsync(database, keys, Path.Combine(root, "drills"));

        Assert.Equal(1, report.RowCounts["WaitingTimes"]);
        Assert.Equal(1, report.RowCounts["WaitingTimeHistoryEntries"]);
        Assert.Equal(1, report.RowCounts["PortalSettings"]);
        Assert.True(report.KeyContinuityVerified);
        Assert.True(report.KeyFiles > 0);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(database)));
        var review = Assert.Single(await DatabaseMaintenance.ReviewDemoHistoryAsync(database));
        Assert.Equal("Real record", review.Name);
        Assert.True(review.IsActive);
        Assert.Equal(1, review.SuspectEntries);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(database)));

        // A wrong key archive must fail instead of silently creating replacement keys.
        var wrongKeys = Path.Combine(root, "wrong.keys");
        Directory.CreateDirectory(wrongKeys);
        DataProtectionProvider.Create(new DirectoryInfo(wrongKeys), builder => builder.SetApplicationName("WT.Blazor"))
            .CreateProtector("fixture").Protect("unrelated token");
        await Assert.ThrowsAnyAsync<Exception>(() => DatabaseMaintenance.VerifyBackupAsync(database, wrongKeys, Path.Combine(root, "drills")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseMaintenance.VerifyBackupAsync(database, Path.Combine(root, "missing"), root));
        await Assert.ThrowsAsync<FileNotFoundException>(() => DatabaseMaintenance.ReviewDemoHistoryAsync(Path.Combine(root, "missing.db")));
    }
}
