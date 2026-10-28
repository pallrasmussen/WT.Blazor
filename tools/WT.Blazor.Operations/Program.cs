using System.Text.Json;
using WT.Blazor.Services;

try
{
    object result = args switch
    {
        ["snapshot-database", var database, var destination] => await DatabaseMaintenance.SnapshotDatabaseAsync(database, destination),
        ["review-demo", var database] => await DatabaseMaintenance.ReviewDemoHistoryAsync(database),
        ["backup-probe", var keys] => new { protectedValue = DatabaseMaintenance.CreateRecoveryProbe(keys) },
        ["verify-backup", var database, var keys, var work] => await DatabaseMaintenance.VerifyBackupAsync(database, keys, work),
        _ => throw new ArgumentException("Usage: snapshot-database <source> <new-backup> | review-demo <database> | backup-probe <live-key-ring> | verify-backup <database-backup> <matching-key-archive> <isolated-work-directory>")
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
