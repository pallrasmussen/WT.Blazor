using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class TrendReadService(ApplicationDbContext db, TimeProvider timeProvider) : ITrendReadService
{
    public async Task<WaitingTime?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        return await db.WaitingTimes.AsNoTracking().FirstOrDefaultAsync(waitingTime => waitingTime.Id == id, cancellationToken);
    }

    public async Task<List<TrendPoint>> GetTrendPointsAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        if (periodDays is <= 0) throw new ArgumentOutOfRangeException(nameof(periodDays), "Trend periods must contain at least one day.");

        var definition = await db.WaitingTimes.AsNoTracking()
            .Where(waitingTime => waitingTime.Id == waitingTimeId)
            .Select(waitingTime => new { waitingTime.Unit, waitingTime.EstimateSeries, waitingTime.EstimateStatus, waitingTime.WaitUpperValue })
            .FirstOrDefaultAsync(cancellationToken);
        if (definition is null || definition.EstimateStatus != EstimateStatus.Available || definition.WaitUpperValue != null) return [];
        var displayUnit = definition.Unit;

        var historyEntries = db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => entry.WaitingTimeId == waitingTimeId && entry.EstimateSeries == definition.EstimateSeries && (entry.Action == HistoryAction.Created || entry.Action == HistoryAction.Updated) && entry.NewWaitValue != null);

        if (periodDays is null)
        {
            var history = await historyEntries
                .OrderBy(entry => entry.Timestamp).ThenBy(entry => entry.Id)
                .Select(entry => new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays))
                .ToListAsync(cancellationToken);
            return TrendPointReducer.Reduce(history, displayUnit);
        }

        var windowEnd = timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = windowEnd.AddDays(-periodDays.Value);
        historyEntries = historyEntries.Where(entry => entry.Timestamp <= windowEnd);
        var latestPointBeforeCutoff = await historyEntries
            .Where(entry => entry.Timestamp < cutoff)
            .OrderByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id)
            .Take(1)
            .Select(entry => new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays))
            .ToListAsync(cancellationToken);
        var pointsInPeriod = await historyEntries
            .Where(entry => entry.Timestamp >= cutoff)
            .OrderBy(entry => entry.Timestamp).ThenBy(entry => entry.Id)
            .Select(entry => new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays))
            .ToListAsync(cancellationToken);

        return TrendPointReducer.Reduce(latestPointBeforeCutoff.Concat(pointsInPeriod), displayUnit)
            .Where(point => point.Timestamp >= cutoff)
            .ToList();
    }

    public async Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var ids = waitingTimeIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var history = db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => ids.Contains(entry.WaitingTimeId) && db.WaitingTimes.Any(item => item.Id == entry.WaitingTimeId
                    && item.EstimateStatus == EstimateStatus.Available && item.WaitUpperValue == null && item.EstimateSeries == entry.EstimateSeries) && (entry.Action == HistoryAction.Created || entry.Action == HistoryAction.Updated) && entry.NewWaitValue != null);
        // Keep the last entry on each UTC day, including the ID tie-breaker.
        var daily = history.Where(entry => !history.Any(other =>
                other.WaitingTimeId == entry.WaitingTimeId && other.Timestamp.Date == entry.Timestamp.Date &&
                (other.Timestamp > entry.Timestamp || other.Timestamp == entry.Timestamp && other.Id > entry.Id)))
            .Select(entry => new
            {
                entry.WaitingTimeId,
                entry.Timestamp,
                Days = entry.NewWaitDays ?? entry.NewWaitValue!.Value *
                    (entry.NewUnit == WaitUnit.Vikur ? 7 : entry.NewUnit == WaitUnit.Manadir ? 30 : 1)
            });
        // Equal consecutive daily values collapse. The previous *different* daily
        // value gives the same direction as TrendPointReducer.LatestChange without
        // transferring the entire audit history to every public Blazor circuit.
        var changes = await daily.Where(point => !daily.Any(other =>
                other.WaitingTimeId == point.WaitingTimeId && other.Timestamp > point.Timestamp))
            // A single published value cannot establish a direction.
            .Where(point => daily.Any(previous => previous.WaitingTimeId == point.WaitingTimeId && previous.Timestamp < point.Timestamp))
            .Select(point => new
            {
                point.WaitingTimeId,
                Change = point.Days - (daily.Where(previous => previous.WaitingTimeId == point.WaitingTimeId
                        && previous.Timestamp < point.Timestamp && previous.Days != point.Days)
                    .OrderByDescending(previous => previous.Timestamp)
                    .Select(previous => (int?)previous.Days).FirstOrDefault() ?? point.Days)
            }).ToListAsync();

        return changes.ToDictionary(point => point.WaitingTimeId, point => point.Change);
    }
}
