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
    public async Task TrendsIncludeCreationButRecentWindowsDoNotMoveBackToStaleUpdates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var item = new WaitingTime { Name = "Baseline", Hospital = Hospital.Klaksvikar, Unit = WaitUnit.Dagar };
        db.WaitingTimes.Add(item);
        await db.SaveChangesAsync();
        var created = History(item, new DateTime(2026, 1, 1), 20);
        created.Action = HistoryAction.Created;
        db.WaitingTimeHistoryEntries.AddRange(created, History(item, new DateTime(2026, 2, 1), 10));
        await db.SaveChangesAsync();
        var service = new WaitingTimeReadService(db, new FixedTimeProvider());
        var all = await service.GetTrendPointsAsync(item.Id);
        Assert.Equal(2, all.Count);
        Assert.Equal(20, all[0].Value);
        Assert.Empty(await service.GetTrendPointsAsync(item.Id, periodDays: 90));
        Assert.Equal(-10, (await service.GetTrendChangesAsync([item.Id]))[item.Id]);
        db.WaitingTimeHistoryEntries.AddRange(History(item, new DateTime(2026, 9, 1), 8), History(item, new DateTime(2027, 1, 1), 99));
        await db.SaveChangesAsync();
        var recent = Assert.Single(await service.GetTrendPointsAsync(item.Id, periodDays: 90));
        Assert.Equal(8, recent.Value);
    }

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

        var service = new WaitingTimeReadService(db, new FixedTimeProvider());
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);
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
