using WT.Blazor.Models;

namespace WT.Blazor.Services;

public record HistoryExportRow(
    string Timestamp,
    string Hospital,
    string Department,
    string Name,
    string Action,
    string Detail,
    string OldWait,
    string NewWait,
    string Username,
    string RequestId)
{
    public static HistoryExportRow From(WaitingTimeHistoryEntry entry) => new(
        entry.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
        DisplayNames.For(entry.Hospital),
        entry.Department is null ? string.Empty : DisplayNames.For(entry.Department.Value),
        entry.Name,
        DisplayNames.For(entry.Action),
        entry.Detail ?? string.Empty,
        WaitText(entry.OldWaitValue, entry.OldUnit),
        entry.NewWaitValue is null && entry.EstimateStatus != EstimateStatus.Available ? ReferralEstimate.StatusLabel(entry.EstimateStatus) : WaitText(entry.NewWaitValue, entry.NewUnit),
        entry.Username ?? string.Empty,
        entry.RequestId ?? string.Empty);

    private static string WaitText(int? value, WaitUnit? unit) =>
        value is null ? string.Empty : $"{value} {(unit is not null ? DisplayNames.For(unit.Value, value.Value) : string.Empty)}".TrimEnd();
}