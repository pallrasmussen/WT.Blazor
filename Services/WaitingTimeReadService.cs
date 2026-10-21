using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public class WaitingTimeReadService(
    ApplicationDbContext db,
    TimeProvider timeProvider) : IWaitingTimeCatalogService, IHistoryReadService, IOperationsReadService, ITrendReadService
{
    public async Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var query = db.WaitingTimes.AsNoTracking().Where(waitingTime => waitingTime.IsActive);
        if (hospital is not null) query = query.Where(waitingTime => waitingTime.Hospital == hospital);
        if (department is not null) query = query.Where(waitingTime => waitingTime.Department == department);
        return await query.OrderBy(waitingTime => waitingTime.Name).ToListAsync();
    }

    public async Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var query = db.WaitingTimes.AsNoTracking();
        if (hospital is not null) query = query.Where(waitingTime => waitingTime.Hospital == hospital);
        if (department is not null) query = query.Where(waitingTime => waitingTime.Department == department);
        return await query.OrderBy(waitingTime => waitingTime.Name).ToListAsync();
    }

    public async Task<WaitingTime?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        return await db.WaitingTimes.AsNoTracking().FirstOrDefaultAsync(waitingTime => waitingTime.Id == id, cancellationToken);
    }

    public async Task<List<WaitingTimeHistoryEntry>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        return await SortHistory(FilterHistory(query), query.SortBy, query.Descending).ToListAsync(cancellationToken);
    }

    public async Task<HistoryPage> GetHistoryPageAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        var filteredQuery = FilterHistory(query);
        var totalCount = await filteredQuery.CountAsync(cancellationToken);
        var entries = await SortHistory(filteredQuery, query.SortBy, query.Descending)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new HistoryPage(entries, totalCount);
    }

    public async Task<List<string>> GetHistoryNamesAsync()
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        return await db.WaitingTimeHistoryEntries.Select(entry => entry.Name).Distinct().OrderBy(name => name).ToListAsync();
    }

    public async Task<List<string>> GetHistoryUsersAsync()
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        return await db.WaitingTimeHistoryEntries
            .Where(entry => !string.IsNullOrEmpty(entry.Username))
            .Select(entry => entry.Username!)
            .Distinct()
            .OrderBy(username => username)
            .ToListAsync();
    }

    public async Task<OperationsSummary> GetOperationsSummaryAsync(int retentionDays, int greenMaximumAgeMonths, int longWaitMonths)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reviewCutoff = UpdatedStatus.ReviewCutoff(now, greenMaximumAgeMonths);
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
                NeedsReview = group.Count(waitingTime => waitingTime.UpdatedAt <= reviewCutoff),
                LongWaits = group.Count(waitingTime =>
                    waitingTime.Unit == WaitUnit.Dagar && waitingTime.WaitValue > maxDays
                    || waitingTime.Unit == WaitUnit.Vikur && waitingTime.WaitValue > maxWeeks
                    || waitingTime.Unit == WaitUnit.Manadir && waitingTime.WaitValue > maxMonths),
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

    public async Task<List<TrendPoint>> GetTrendPointsAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        if (periodDays is <= 0) throw new ArgumentOutOfRangeException(nameof(periodDays), "Trend periods must contain at least one day.");

        var displayUnit = await db.WaitingTimes.AsNoTracking()
            .Where(waitingTime => waitingTime.Id == waitingTimeId)
            .Select(waitingTime => (WaitUnit?)waitingTime.Unit)
            .FirstOrDefaultAsync(cancellationToken);
        if (displayUnit is null) return [];

        var historyEntries = db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => entry.WaitingTimeId == waitingTimeId && (entry.Action == HistoryAction.Created || entry.Action == HistoryAction.Updated) && entry.NewWaitValue != null);

        if (periodDays is null)
        {
            var history = await historyEntries
                .OrderBy(entry => entry.Timestamp).ThenBy(entry => entry.Id)
                .Select(entry => new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays))
                .ToListAsync(cancellationToken);
            return TrendPointReducer.Reduce(history, displayUnit.Value);
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

        return TrendPointReducer.Reduce(latestPointBeforeCutoff.Concat(pointsInPeriod), displayUnit.Value)
            .Where(point => point.Timestamp >= cutoff)
            .ToList();
    }

    public async Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var ids = waitingTimeIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var reviews = await db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => ids.Contains(entry.WaitingTimeId) && entry.Action == HistoryAction.Reviewed)
            .OrderByDescending(entry => entry.Timestamp)
            .Select(entry => new { entry.WaitingTimeId, entry.Timestamp, entry.Username })
            .ToListAsync();

        return reviews.GroupBy(review => review.WaitingTimeId)
            .ToDictionary(group => group.Key, group => new ReviewInfo(group.First().Timestamp, group.First().Username));
    }

    public async Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var ids = waitingTimeIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var history = db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => ids.Contains(entry.WaitingTimeId) && (entry.Action == HistoryAction.Created || entry.Action == HistoryAction.Updated) && entry.NewWaitValue != null);
        // Only the first and last update determine the indicator. Keep historical
        // rows in SQLite rather than materializing the entire audit trail.
        var first = await history.Where(entry => entry.Id == history
                .Where(candidate => candidate.WaitingTimeId == entry.WaitingTimeId)
                .OrderBy(candidate => candidate.Timestamp).ThenBy(candidate => candidate.Id)
                .Select(candidate => candidate.Id).First())
            .Select(entry => new { entry.WaitingTimeId, Point = new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays) })
            .ToListAsync();
        var last = await history.Where(entry => entry.Id == history
                .Where(candidate => candidate.WaitingTimeId == entry.WaitingTimeId)
                .OrderByDescending(candidate => candidate.Timestamp).ThenByDescending(candidate => candidate.Id)
                .Select(candidate => candidate.Id).First())
            .Select(entry => new { entry.WaitingTimeId, Point = new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays) })
            .ToListAsync();

        var latest = last.ToDictionary(entry => entry.WaitingTimeId, entry => entry.Point);
        return first.ToDictionary(entry => entry.WaitingTimeId,
            entry => TrendPointReducer.Change([entry.Point, latest[entry.WaitingTimeId]]));
    }

    private IQueryable<WaitingTimeHistoryEntry> FilterHistory(HistoryQuery historyQuery)
    {
        var query = db.WaitingTimeHistoryEntries.AsNoTracking();
        if (historyQuery.Hospital is not null) query = query.Where(entry => entry.Hospital == historyQuery.Hospital);
        if (historyQuery.Department is not null) query = query.Where(entry => entry.Department == historyQuery.Department);
        if (!string.IsNullOrEmpty(historyQuery.Name)) query = query.Where(entry => entry.Name == historyQuery.Name);
        if (historyQuery.Action is not null) query = query.Where(entry => entry.Action == historyQuery.Action);
        if (!string.IsNullOrEmpty(historyQuery.Username)) query = query.Where(entry => entry.Username == historyQuery.Username);
        if (historyQuery.FromUtc is not null) query = query.Where(entry => entry.Timestamp >= historyQuery.FromUtc);
        if (historyQuery.ToUtc is not null) query = query.Where(entry => entry.Timestamp < historyQuery.ToUtc);
        if (!string.IsNullOrWhiteSpace(historyQuery.Search))
        {
            var term = historyQuery.Search.Trim();
            query = query.Where(entry => entry.Name.Contains(term)
                || (entry.Detail != null && entry.Detail.Contains(term))
                || (entry.Username != null && entry.Username.Contains(term)));
        }
        return query;
    }

    private static IOrderedQueryable<WaitingTimeHistoryEntry> SortHistory(IQueryable<WaitingTimeHistoryEntry> query, string? sortBy, bool descending) =>
        (sortBy?.ToLowerInvariant(), descending) switch
        {
            ("name", true) => query.OrderByDescending(entry => entry.Name).ThenByDescending(entry => entry.Timestamp),
            ("name", false) => query.OrderBy(entry => entry.Name).ThenByDescending(entry => entry.Timestamp),
            ("action", true) => query.OrderByDescending(entry => entry.Action).ThenByDescending(entry => entry.Timestamp),
            ("action", false) => query.OrderBy(entry => entry.Action).ThenByDescending(entry => entry.Timestamp),
            ("username", true) => query.OrderByDescending(entry => entry.Username).ThenByDescending(entry => entry.Timestamp),
            ("username", false) => query.OrderBy(entry => entry.Username).ThenByDescending(entry => entry.Timestamp),
            (_, false) => query.OrderBy(entry => entry.Timestamp),
            _ => query.OrderByDescending(entry => entry.Timestamp)
        };
}