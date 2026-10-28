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
    public async Task SnapshotIncludesCommittedWalDataAndRefusesToOverwriteAnyFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wt-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "live.db");
        var backup = Path.Combine(root, "backup.db");
        await using var connection = new SqliteConnection($"Data Source={source};Pooling=False");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE Fixture (Value TEXT); INSERT INTO Fixture VALUES ('committed WAL data');";
        await command.ExecuteNonQueryAsync();
        Assert.True(File.Exists(source + "-wal"));
        await DatabaseMaintenance.SnapshotDatabaseAsync(source, backup);
        await using var restored = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
        await restored.OpenAsync();
        using var check = restored.CreateCommand();
        check.CommandText = "SELECT Value FROM Fixture";
        Assert.Equal("committed WAL data", await check.ExecuteScalarAsync());
        await Assert.ThrowsAsync<IOException>(() => DatabaseMaintenance.SnapshotDatabaseAsync(source, backup));
        await Assert.ThrowsAsync<IOException>(() => DatabaseMaintenance.SnapshotDatabaseAsync(source, source));
        Assert.True(File.Exists(source));
    }

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
            db.PortalSettings.Add(new PortalSettings { EstimateExpiryMonths = 9, ContactGuidanceEn = "Recovery contact", PublishPatientGuidance = true });
            var item = new WaitingTime { Name = "Real record", Hospital = Hospital.Klaksvikar, WaitValue = 6, WaitUpperValue = 10, Unit = WaitUnit.Vikur, ResponsibleDepartment = "Imaging", VerifiedAt = DateTime.UtcNow };
            db.WaitingTimes.Add(item);
            await db.SaveChangesAsync();
            db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry { WaitingTimeId = item.Id, Name = item.Name, Username = "demo", Detail = "Demo", NewWaitValue = 6, NewWaitUpperValue = 10, NewUnit = WaitUnit.Vikur });
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
        Assert.True(report.ApplicationDataReadable);
        await using (var restored = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={Path.Combine(report.RestorePath, "wt-blazor.db")};Mode=ReadOnly;Pooling=False").Options))
        {
            var restoredItem = await restored.WaitingTimes.SingleAsync();
            Assert.Equal(6, restoredItem.WaitValue);
            Assert.Equal(10, restoredItem.WaitUpperValue);
            Assert.Equal("Imaging", restoredItem.ResponsibleDepartment);
            Assert.NotNull(restoredItem.VerifiedAt);
            var settings = await restored.PortalSettings.SingleAsync();
            Assert.Equal(9, settings.EstimateExpiryMonths);
            Assert.Equal("Recovery contact", settings.ContactGuidanceEn);
            Assert.True(settings.PublishPatientGuidance);
            Assert.Equal(10, (await restored.WaitingTimeHistoryEntries.SingleAsync()).NewWaitUpperValue);
        }
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
