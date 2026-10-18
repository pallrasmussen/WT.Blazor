using System.ComponentModel.DataAnnotations;

namespace WT.Blazor.Models;

public sealed class BackupOptions
{
    public string Path { get; set; } = @"C:\inetpub\backups\WT.Blazor";
}

public sealed class AuditRetentionOptions
{
    public int ReviewAfterDays { get; set; } = 3650;
}

public sealed class ExportOptions
{
    public const int DefaultMaximumHistoryRows = 10_000;
    public const int DefaultMaximumTrendPoints = 1_000;

    [Range(1, int.MaxValue)]
    public int MaximumHistoryRows { get; set; } = DefaultMaximumHistoryRows;

    [Range(1, int.MaxValue)]
    public int MaximumTrendPoints { get; set; } = DefaultMaximumTrendPoints;

    [Required]
    public string TemporaryPath { get; set; } = Path.Combine("App_Data", "export-temp");

    [Range(1, 720)]
    public int TemporaryFileRetentionHours { get; set; } = 24;

    [Range(1, 168)]
    public int TemporaryFileCleanupIntervalHours { get; set; } = 1;
}

public sealed class HostingOptions
{
    public bool RedirectToHttps { get; set; }
}