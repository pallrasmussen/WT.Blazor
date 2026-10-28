using WT.Blazor.Services.Exports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WT.Blazor.Models;
namespace WT.Blazor.Services;

public interface ITrendExportService
{
    Task<ExportFile?> CreatePdfAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null);
}

public sealed class TrendExportService(
    ITrendReportService trendReportService,
    TimeProvider timeProvider,
    IOptions<ExportOptions> exportOptions,
    ILogger<TrendExportService> logger,
    TrendPdfRenderer renderer) : ITrendExportService
{
    public async Task<ExportFile?> CreatePdfAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
    {
        if (!TrendPeriod.IsSupported(periodDays)) throw new ArgumentOutOfRangeException(nameof(periodDays));
        var startedAt = timeProvider.GetTimestamp();
        var report = await trendReportService.GetAsync(waitingTimeId, cancellationToken, periodDays);
        if (report is null) return null;

        var maximumPoints = exportOptions.Value.MaximumTrendPoints;
        if (maximumPoints < 1) throw new InvalidOperationException("Exports:MaximumTrendPoints must be greater than zero.");
        if (report.Points.Count > maximumPoints)
        {
            logger.LogWarning("Trend PDF export rejected for waiting time {WaitingTimeId}: {PointCount} points exceeds limit {MaximumPoints}", waitingTimeId, report.Points.Count, maximumPoints);
            throw new TrendExportLimitExceededException(maximumPoints);
        }

        var generatedAt = timeProvider.GetLocalNow().DateTime;
        var file = renderer.Render(report, waitingTimeId, generatedAt, periodDays);
        return ExportFile.FromWriter(file.ContentType, file.FileName, async (stream, token) =>
        {
            await file.WriteToAsync(stream, token);
            logger.LogInformation("Created trend PDF export for waiting time {WaitingTimeId} with {PointCount} points in {ElapsedMilliseconds} ms", waitingTimeId, report.Points.Count, timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        });
    }
}
