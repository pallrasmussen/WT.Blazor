using WT.Blazor.Models;

namespace WT.Blazor.Services;

public record TrendHistoryPoint(DateTime Timestamp, int Value, WaitUnit? Unit, int? WaitDays);

public static class TrendPointReducer
{
    public static List<TrendPoint> Reduce(IEnumerable<TrendHistoryPoint> history, WaitUnit displayUnit)
    {
        var latestByDay = history
            .Select(point => new TrendPoint(point.Timestamp, WaitDuration.FromDays(CanonicalDays(point), displayUnit)))
            .GroupBy(point => point.Timestamp.Date)
            .Select(group => group.OrderBy(point => point.Timestamp).Last())
            .OrderBy(point => point.Timestamp)
            .ToList();

        var collapsed = new List<TrendPoint>();
        foreach (var point in latestByDay)
        {
            if (collapsed.Count == 0 || collapsed[^1].Value != point.Value)
                collapsed.Add(point);
            else
                collapsed[^1] = new TrendPoint(point.Timestamp, point.Value);
        }
        return collapsed;
    }

    public static int Change(IEnumerable<TrendHistoryPoint> history)
    {
        var points = history.OrderBy(point => point.Timestamp).ToList();
        return points.Count == 0 ? 0 : CanonicalDays(points[^1]) - CanonicalDays(points[0]);
    }

    private static int CanonicalDays(TrendHistoryPoint point) =>
        point.WaitDays ?? WaitDuration.ToDays(point.Value, point.Unit ?? throw new InvalidOperationException("A trend point requires a unit or canonical days."));
}