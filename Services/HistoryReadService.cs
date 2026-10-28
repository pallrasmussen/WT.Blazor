using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class HistoryReadService(ApplicationDbContext db) : IHistoryReadService
{
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

    public async Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var ids = waitingTimeIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var reviews = db.WaitingTimeHistoryEntries.AsNoTracking()
            .Where(entry => ids.Contains(entry.WaitingTimeId) && entry.Action == HistoryAction.Reviewed);
        var latest = await reviews.Where(entry => !reviews.Any(other =>
                other.WaitingTimeId == entry.WaitingTimeId &&
                (other.Timestamp > entry.Timestamp || other.Timestamp == entry.Timestamp && other.Id > entry.Id)))
            .Select(entry => new { entry.WaitingTimeId, entry.Timestamp, entry.Username })
            .ToListAsync();

        return latest.ToDictionary(review => review.WaitingTimeId, review => new ReviewInfo(review.Timestamp, review.Username));
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
            ("name", true) => query.OrderByDescending(entry => entry.Name).ThenByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id),
            ("name", false) => query.OrderBy(entry => entry.Name).ThenByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id),
            ("action", true) => query.OrderByDescending(entry => entry.Action).ThenByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id),
            ("action", false) => query.OrderBy(entry => entry.Action).ThenByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id),
            ("username", true) => query.OrderByDescending(entry => entry.Username).ThenByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id),
            ("username", false) => query.OrderBy(entry => entry.Username).ThenByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id),
            (_, false) => query.OrderBy(entry => entry.Timestamp).ThenBy(entry => entry.Id),
            _ => query.OrderByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id)
        };
}
