using WT.Blazor.Models;

namespace WT.Blazor.Services;

public record TrendReport(
    int WaitingTimeId,
    string Name,
    string Hospital,
    string? Department,
    string CurrentWait,
    string UnitLabel,
    List<TrendPoint> Points);

public interface ITrendReportService
{
    Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null);
}

public sealed class TrendReportService(ITrendReadService trendReader) : ITrendReportService
{
    public async Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
    {
        var item = await trendReader.GetByIdAsync(waitingTimeId, cancellationToken);
        if (item is null) return null;

        var points = await trendReader.GetTrendPointsAsync(waitingTimeId, cancellationToken, periodDays);
        return new TrendReport(
            item.Id,
            item.Name,
            DisplayNames.For(item.Hospital),
            item.Department is null ? null : DisplayNames.For(item.Department.Value),
            $"{item.WaitValue} {DisplayNames.For(item.Unit, item.WaitValue)}",
            DisplayNames.For(item.Unit, item.WaitValue),
            points);
    }
}