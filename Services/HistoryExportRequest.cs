namespace WT.Blazor.Services;

public sealed class HistoryExportRequest
{
    public string? Hospital { get; init; }
    public string? Department { get; init; }
    public string? Name { get; init; }
    public string? Action { get; init; }
    public string? Username { get; init; }
    public string? Q { get; init; }
    public string? Sort { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }

    public HistoryQuery ToHistoryQuery() =>
        HistoryQueryMapper.FromExportParameters(Hospital, Department, Name, Action, Username, Q, Sort, From, To);
}