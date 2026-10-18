using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class TrendPointReducerTests
{
    [Fact]
    public void ReduceUsesTheLatestPointEachDayAndCollapsesUnchangedValues()
    {
        var points = TrendPointReducer.Reduce(
        [
            new TrendHistoryPoint(new DateTime(2026, 9, 1, 8, 0, 0), 2, WaitUnit.Vikur, null),
            new TrendHistoryPoint(new DateTime(2026, 9, 1, 16, 0, 0), 3, WaitUnit.Vikur, null),
            new TrendHistoryPoint(new DateTime(2026, 9, 2, 8, 0, 0), 21, WaitUnit.Dagar, null),
            new TrendHistoryPoint(new DateTime(2026, 9, 3, 8, 0, 0), 1, WaitUnit.Manadir, 30)
        ], WaitUnit.Dagar);

        Assert.Equal([new TrendPoint(new DateTime(2026, 9, 2, 8, 0, 0), 21), new TrendPoint(new DateTime(2026, 9, 3, 8, 0, 0), 30)], points);
    }

    [Fact]
    public void ChangeUsesCanonicalDaysAcrossUnits()
    {
        var change = TrendPointReducer.Change(
        [
            new TrendHistoryPoint(new DateTime(2026, 9, 1), 2, WaitUnit.Vikur, null),
            new TrendHistoryPoint(new DateTime(2026, 9, 2), 1, WaitUnit.Manadir, null)
        ]);

        Assert.Equal(16, change);
    }
}