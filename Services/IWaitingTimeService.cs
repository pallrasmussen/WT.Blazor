using WT.Blazor.Models;

namespace WT.Blazor.Services;

public record TrendPoint(DateTime Timestamp, int Value);
public record HistoryPage(IReadOnlyList<WaitingTimeHistoryEntry> Entries, int TotalCount);
public record ReviewInfo(DateTime Timestamp, string? Username);
public record OperationsSummary(int EntriesForReview, int RecentChanges, int NeedsReview, int LongWaits, int MissingDepartments);
public sealed record WaitingTimeCreate : WaitingTimeInput
{
    public WaitingTimeCreate() { }
    public WaitingTimeCreate(WaitingTimeInput input) : base(input) { }
    public required string Username { get; init; }
}

public sealed record WaitingTimeUpdate : WaitingTimeInput
{
    public WaitingTimeUpdate() { }
    public WaitingTimeUpdate(WaitingTimeInput input) : base(input) { }
    public required int Id { get; init; }
    public required int ExpectedVersion { get; init; }
    public required string Username { get; init; }
    public bool ConfirmLargeChange { get; init; }
}

public record HistoryQuery(
    Hospital? Hospital = null,
    Department? Department = null,
    string? Name = null,
    HistoryAction? Action = null,
    string? Username = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null,
    string? SortBy = null,
    bool Descending = true,
    int PageNumber = 1,
    int PageSize = 50);

public interface IWaitingTimeCatalogService
{
    Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department);
    Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department);
}

public interface IHistoryReadService
{
    Task<List<WaitingTimeHistoryEntry>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default);
    Task<HistoryPage> GetHistoryPageAsync(HistoryQuery query, CancellationToken cancellationToken = default);
    Task<List<string>> GetHistoryNamesAsync();
    Task<List<string>> GetHistoryUsersAsync();
    Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds);
}

public interface IOperationsReadService
{
    Task<OperationsSummary> GetOperationsSummaryAsync(int retentionDays, int greenMaximumAgeMonths, int longWaitMonths, int estimateExpiryMonths = 12);
}

public interface ITrendReadService
{
    Task<WaitingTime?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<TrendPoint>> GetTrendPointsAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null);
    Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds);
}

public interface IWaitingTimeManagementService
{
    Task<WaitingTime> CreateAsync(WaitingTimeCreate create);
    Task<WaitingTime> UpdateAsync(WaitingTimeUpdate update);
    Task<WaitingTime> MarkReviewedAsync(int id, int expectedVersion, string username);
    Task<WaitingTime> DeactivateAsync(int id, int expectedVersion, string username, string? reason = null);
    Task<WaitingTime> ReactivateAsync(int id, int expectedVersion, string username);
}
