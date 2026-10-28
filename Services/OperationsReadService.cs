using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class OperationsReadService(ApplicationDbContext db, TimeProvider timeProvider) : IOperationsReadService
{
    public async Task<OperationsSummary> GetOperationsSummaryAsync(int retentionDays, int greenMaximumAgeMonths, int longWaitMonths, int estimateExpiryMonths = 12)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reviewCutoff = UpdatedStatus.ReviewCutoff(now, Math.Min(greenMaximumAgeMonths, estimateExpiryMonths));
        var longWaitDays = WaitDuration.ToDays(longWaitMonths, WaitUnit.Manadir);
        var maxDays = WaitDuration.MaximumValueAtOrBelowDays(longWaitDays, WaitUnit.Dagar);
        var maxWeeks = WaitDuration.MaximumValueAtOrBelowDays(longWaitDays, WaitUnit.Vikur);
        var maxMonths = WaitDuration.MaximumValueAtOrBelowDays(longWaitDays, WaitUnit.Manadir);
        var historyCounts = await db.WaitingTimeHistoryEntries
            .GroupBy(_ => 1)
            .Select(group => new
            {
                EntriesForReview = group.Count(entry => entry.Timestamp < now.AddDays(-retentionDays)),
                RecentChanges = group.Count(entry => entry.Timestamp >= now.AddDays(-7))
            })
            .FirstOrDefaultAsync();
        var waitingTimeCounts = await db.WaitingTimes.AsNoTracking()
            .Where(waitingTime => waitingTime.IsActive)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                NeedsReview = group.Count(waitingTime => (waitingTime.VerifiedAt ?? (waitingTime.EstimateStatus == EstimateStatus.Available ? DateTime.MinValue : waitingTime.UpdatedAt)) <= reviewCutoff
                    || waitingTime.EstimateStatus == EstimateStatus.Available && waitingTime.VerifiedAt > now
                    || waitingTime.ResponsibleDepartment == null || waitingTime.ResponsibleDepartment.Trim() == ""
                    || waitingTime.ReferralEndpoint == ReferralEndpoint.Unspecified),
                LongWaits = group.Count(waitingTime => waitingTime.EstimateStatus == EstimateStatus.Available && (
                    waitingTime.Unit == WaitUnit.Dagar && (waitingTime.WaitUpperValue ?? waitingTime.WaitValue) > maxDays
                    || waitingTime.Unit == WaitUnit.Vikur && (waitingTime.WaitUpperValue ?? waitingTime.WaitValue) > maxWeeks
                    || waitingTime.Unit == WaitUnit.Manadir && (waitingTime.WaitUpperValue ?? waitingTime.WaitValue) > maxMonths)),
                MissingDepartments = group.Count(waitingTime => waitingTime.Hospital == Hospital.Landssjukrahusid && waitingTime.Department == null)
            })
            .FirstOrDefaultAsync();
        return new OperationsSummary(
            historyCounts?.EntriesForReview ?? 0,
            historyCounts?.RecentChanges ?? 0,
            waitingTimeCounts?.NeedsReview ?? 0,
            waitingTimeCounts?.LongWaits ?? 0,
            waitingTimeCounts?.MissingDepartments ?? 0);
    }
}
