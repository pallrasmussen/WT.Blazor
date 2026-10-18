using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class ExportTemporaryFileCleanupService(
    IOptions<ExportOptions> exportOptions,
    TimeProvider timeProvider,
    ILogger<ExportTemporaryFileCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Cleanup();
        using var timer = new PeriodicTimer(TimeSpan.FromHours(exportOptions.Value.TemporaryFileCleanupIntervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken)) Cleanup();
    }

    private void Cleanup()
    {
        try
        {
            var result = ExportTemporaryStorage.CleanupExpired(exportOptions.Value, timeProvider.GetUtcNow().UtcDateTime);
            if (result.FilesRemoved > 0)
                logger.LogInformation("Removed {FileCount} stale export files totaling {ByteCount} bytes", result.FilesRemoved, result.BytesRemoved);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to clean stale export files from {TemporaryPath}", exportOptions.Value.TemporaryPath);
        }
    }
}