using WT.Blazor.Localization;
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
        WaitText(entry.OldWaitValue, entry.OldUnit, entry.OldWaitUpperValue),
        entry.NewWaitValue is null && entry.EstimateStatus != EstimateStatus.Available ? ReferralEstimateText.StatusLabel(entry.EstimateStatus) : WaitText(entry.NewWaitValue, entry.NewUnit, entry.NewWaitUpperValue),
        entry.Username ?? string.Empty,
        entry.RequestId ?? string.Empty);

    private static string WaitText(int? value, WaitUnit? unit, int? upper = null) =>
        value is null ? string.Empty : $"{(upper is null ? value.ToString() : $"{value}–{upper}")} {(unit is not null ? DisplayNames.For(unit.Value, value.Value) : string.Empty)}".TrimEnd();
}