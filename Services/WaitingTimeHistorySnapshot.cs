using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed record HistoryActor(string RequestId, DateTime Timestamp, string Username);

public sealed record WaitingTimeDurationSnapshot(int? Value, int? UpperValue, WaitUnit Unit, int? Days)
{
    public static WaitingTimeDurationSnapshot Capture(WaitingTime item) => new(
        item.EstimateStatus == EstimateStatus.Available ? item.WaitValue : null,
        item.WaitUpperValue,
        item.Unit,
        item.EstimateStatus == EstimateStatus.Available ? WaitDuration.ToDays(item.WaitValue, item.Unit) : null);
}

public static class WaitingTimeHistorySnapshot
{
    public static WaitingTimeHistoryEntry Create(WaitingTime item, HistoryActor actor, HistoryAction action,
        string detail, WaitingTimeDurationSnapshot? previous = null, bool includeCurrent = true)
    {
        var entry = new WaitingTimeHistoryEntry
        {
            WaitingTimeId = item.Id,
            RequestId = actor.RequestId,
            Timestamp = actor.Timestamp,
            Username = actor.Username,
            Hospital = item.Hospital,
            Department = item.Department,
            Name = item.Name,
            EstimateStatus = item.EstimateStatus,
            ReferralEndpoint = item.ReferralEndpoint,
            ResponsibleDepartment = item.ResponsibleDepartment,
            ComparisonCode = item.ComparisonCode,
            EstimateSeries = item.EstimateSeries,
            // Preserve the existing snapshot metadata even on deactivation entries,
            // which have no new numeric value or unit.
            NewWaitUpperValue = item.WaitUpperValue,
            Action = action,
            Detail = detail
        };
        if (previous is not null)
        {
            entry.OldWaitValue = previous.Value;
            entry.OldWaitUpperValue = previous.UpperValue;
            entry.OldUnit = previous.Unit;
            entry.OldWaitDays = previous.Days;
        }
        if (includeCurrent)
        {
            var current = WaitingTimeDurationSnapshot.Capture(item);
            entry.NewWaitValue = current.Value;
            entry.NewWaitUpperValue = current.UpperValue;
            entry.NewUnit = current.Unit;
            entry.NewWaitDays = current.Days;
        }
        return entry;
    }
}
