using WT.Blazor.Models;

namespace WT.Blazor.Services;

public record TrendReport(
    int WaitingTimeId,
    string Name,
    string Hospital,
    string? Department,
    string CurrentWait,
    string UnitLabel,
    List<TrendPoint> Points,
    string? ReferralDefinition = null,
    DateTime? VerifiedAt = null,
    string? Conditions = null);

public interface ITrendReportService
{
    Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null);
}

public sealed class TrendReportService(ITrendReadService trendReader) : ITrendReportService
{
    public async Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
    {
        var item = await trendReader.GetByIdAsync(waitingTimeId, cancellationToken);
        if (item is null || !item.IsActive) return null;

        var points = await trendReader.GetTrendPointsAsync(waitingTimeId, cancellationToken, periodDays);
        return new TrendReport(
            item.Id,
            item.Name,
            DisplayNames.For(item.Hospital),
            item.Department is null ? null : DisplayNames.For(item.Department.Value),
            ReferralEstimate.WaitText(item),
            DisplayNames.For(item.Unit, item.WaitValue),
            points,
            ReferralEstimate.EndpointLabel(item.ReferralEndpoint),
            item.EstimateStatus == EstimateStatus.Available ? item.VerifiedAt : null,
            item.Note);
    }
}