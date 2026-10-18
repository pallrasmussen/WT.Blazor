using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class TrendReadServiceTests
{
    [Fact]
    public async Task PeriodQueryLoadsOnlyTheSelectedPeriodAndPreservesItsInitialChange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var waitingTime = new WaitingTime { Name = "MR-skanning", Hospital = Hospital.Klaksvikar, WaitValue = 3, Unit = WaitUnit.Dagar };
        db.WaitingTimes.Add(waitingTime);
        await db.SaveChangesAsync();
        db.WaitingTimeHistoryEntries.AddRange(
            History(waitingTime, new DateTime(2026, 1, 1), 7),
            History(waitingTime, new DateTime(2026, 8, 1), 7),
            History(waitingTime, new DateTime(2026, 9, 1), 8),
            History(waitingTime, new DateTime(2026, 9, 2), 8),
            History(waitingTime, new DateTime(2026, 9, 3), 9));
        await db.SaveChangesAsync();

        var service = new WaitingTimeReadService(db, TimeProvider.System);
        var points = await service.GetTrendPointsAsync(waitingTime.Id, periodDays: 90);

        Assert.Equal(
        [
            new TrendPoint(new DateTime(2026, 8, 1), 7),
            new TrendPoint(new DateTime(2026, 9, 2), 8),
            new TrendPoint(new DateTime(2026, 9, 3), 9)
        ],
        points);
    }

    [Fact]
    public async Task TrendQueryHonorsCancellationAndRejectsInvalidPeriods()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new WaitingTimeReadService(db, TimeProvider.System);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetTrendPointsAsync(1, source.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetTrendPointsAsync(1, periodDays: 0));
    }

    private static WaitingTimeHistoryEntry History(WaitingTime waitingTime, DateTime timestamp, int waitValue) =>
        new()
        {
            WaitingTimeId = waitingTime.Id,
            Hospital = waitingTime.Hospital,
            Name = waitingTime.Name,
            Action = HistoryAction.Updated,
            Timestamp = timestamp,
            NewWaitValue = waitValue,
            NewUnit = WaitUnit.Dagar,
            NewWaitDays = waitValue
        };
}
