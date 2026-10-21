using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;

namespace WT.Blazor.Services;

public sealed record DemoHistoryReview(int WaitingTimeId, string Name, bool IsActive, int SuspectEntries, DateTime FirstTimestamp, DateTime LastTimestamp);
public sealed record RecoveryVerification(string RestorePath, Dictionary<string, long> RowCounts, int KeyFiles, bool KeyContinuityVerified);

// Offline operations only: no web endpoints and no changes to the source database or key archive.
public static class DatabaseMaintenance
{
    private const string ProbeValue = "WT.Blazor backup recovery probe v1";

    public static string CreateRecoveryProbe(string keyRingPath) => Protector(keyRingPath).Protect(ProbeValue);

    public static async Task<List<DemoHistoryReview>> ReviewDemoHistoryAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(ReadOnlyConnectionString(databasePath)).Options;
        await using var db = new ApplicationDbContext(options);
        return await db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => entry.Username == "demo" || entry.Detail == "Demo")
            .GroupBy(entry => new { entry.WaitingTimeId, entry.WaitingTime!.Name, entry.WaitingTime.IsActive })
            .OrderBy(group => group.Key.WaitingTimeId)
            .Select(group => new DemoHistoryReview(group.Key.WaitingTimeId, group.Key.Name, group.Key.IsActive,
                group.Count(), group.Min(entry => entry.Timestamp), group.Max(entry => entry.Timestamp)))
            .ToListAsync();
    }

    public static async Task<RecoveryVerification> VerifyBackupAsync(string databasePath, string keyRingPath, string workPath)
    {
        databasePath = Path.GetFullPath(databasePath);
        keyRingPath = Path.GetFullPath(keyRingPath);
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Database backup not found.", databasePath);
        if (!Directory.Exists(keyRingPath) || !Directory.EnumerateFiles(keyRingPath, "*.xml").Any())
            throw new InvalidOperationException("A matching, non-empty Data Protection key archive is required.");
        var probePath = databasePath + ".probe";
        if (!File.Exists(probePath))
            throw new InvalidOperationException("The backup-time recovery probe is missing. Take a new backup with the release operations tool before running a full recovery drill.");

        // A unique directory prevents overwriting an earlier drill or a live deployment.
        var restorePath = Path.Combine(Path.GetFullPath(workPath), $"restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(restorePath);
        var restoredDatabase = Path.Combine(restorePath, "wt-blazor.db");
        File.Copy(databasePath, restoredDatabase);
        var restoredKeys = Path.Combine(restorePath, "keys");
        Directory.CreateDirectory(restoredKeys);
        var keys = Directory.GetFiles(keyRingPath, "*.xml");
        foreach (var key in keys) File.Copy(key, Path.Combine(restoredKeys, Path.GetFileName(key)));

        if (!await HashesMatchAsync(databasePath, restoredDatabase))
            throw new InvalidOperationException("Restored database hash does not match the backup.");
        foreach (var key in keys)
            if (!await HashesMatchAsync(key, Path.Combine(restoredKeys, Path.GetFileName(key))))
                throw new InvalidOperationException("Restored key hash does not match its archive.");

        await using var connection = new SqliteConnection(ReadOnlyConnectionString(restoredDatabase));
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        if (!Equals(await command.ExecuteScalarAsync(), "ok")) throw new InvalidOperationException("SQLite integrity check failed.");
        command.CommandText = "PRAGMA foreign_key_check;";
        await using (var reader = await command.ExecuteReaderAsync())
            if (await reader.ReadAsync()) throw new InvalidOperationException("SQLite foreign key check failed.");

        var counts = new Dictionary<string, long>();
        foreach (var table in new[] { "WaitingTimes", "WaitingTimeHistoryEntries", "PortalSettings", "PortalSettingsAuditEntries", "__EFMigrationsHistory" })
        {
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
            counts[table] = Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        if (counts["PortalSettings"] != 1 || counts["__EFMigrationsHistory"] == 0)
            throw new InvalidOperationException("The backup is missing initialized portal settings or migration history.");

        // The probe was encrypted by the live key ring at backup time. An unrelated
        // but otherwise valid key archive must fail this check.
        var protectedProbe = (await File.ReadAllTextAsync(probePath)).Trim();
        if (Protector(restoredKeys).Unprotect(protectedProbe) != ProbeValue)
            throw new InvalidOperationException("Restored Data Protection keys could not decrypt the recovery probe.");

        return new RecoveryVerification(restorePath, counts, keys.Length, true);
    }

    private static IDataProtector Protector(string path) => DataProtectionProvider.Create(new DirectoryInfo(path), builder =>
        builder.SetApplicationName("WT.Blazor").DisableAutomaticKeyGeneration()).CreateProtector("WT.Blazor.RecoveryVerification.v1");

    private static string ReadOnlyConnectionString(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Database not found.", path);
        return new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
    }

    private static async Task<byte[]> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await SHA256.HashDataAsync(stream);
    }

    private static async Task<bool> HashesMatchAsync(string source, string destination)
    {
        var first = await HashAsync(source);
        var second = await HashAsync(destination);
        return CryptographicOperations.FixedTimeEquals(first, second);
    }
}
