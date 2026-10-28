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
    public async Task DatabaseTrendSummariesMatchDailyReductionWithTiesUnitsAndEqualRuns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var items = Enumerable.Range(0, 4).Select(index => new WaitingTime { Name = $"Record {index}" }).ToList();
        db.WaitingTimes.AddRange(items);
        await db.SaveChangesAsync();
        var timestamp = new DateTime(2025, 1, 1);
        for (var index = 0; index < 500; index++)
        {
            var entry = History(items[0], timestamp.AddDays(index / 2), index < 400 ? index % 17 : 42);
            entry.Action = index == 0 ? HistoryAction.Created : HistoryAction.Updated;
            db.WaitingTimeHistoryEntries.Add(entry);
        }
        // Same timestamp: the final inserted row wins. Legacy unit-only values
        // and canonical days take precedence exactly as in the chart reducer.
        var week = History(items[1], timestamp, 2);
        week.NewUnit = WaitUnit.Vikur;
        week.NewWaitDays = null;
        var month = History(items[1], timestamp.AddDays(1), 1);
        month.NewUnit = WaitUnit.Manadir;
        month.NewWaitDays = null;
        var tie = History(items[1], timestamp.AddDays(1), 10);
        tie.NewWaitDays = 7;
        db.WaitingTimeHistoryEntries.AddRange(week, month, tie, History(items[2], timestamp, 5));
        var ignored = History(items[1], timestamp.AddDays(2), 999);
        ignored.Action = HistoryAction.Reviewed;
        db.WaitingTimeHistoryEntries.Add(ignored);
        await db.SaveChangesAsync();
        var reader = new WaitingTimeReadService(db, TimeProvider.System);
        var changes = await reader.GetTrendChangesAsync(items.Select(item => item.Id).Append(items[0].Id));
        foreach (var item in items.Take(2))
        {
            var history = await db.WaitingTimeHistoryEntries.Where(entry => entry.WaitingTimeId == item.Id
                    && (entry.Action == HistoryAction.Created || entry.Action == HistoryAction.Updated))
                .OrderBy(entry => entry.Timestamp).ThenBy(entry => entry.Id)
                .Select(entry => new TrendHistoryPoint(entry.Timestamp, entry.NewWaitValue!.Value, entry.NewUnit, entry.NewWaitDays)).ToListAsync();
            Assert.Equal(TrendPointReducer.LatestChange(history), changes[item.Id]);
        }
        Assert.Equal(-7, changes[items[1].Id]);
        Assert.False(changes.ContainsKey(items[2].Id));
        Assert.False(changes.ContainsKey(items[3].Id));
        Assert.Empty(await reader.GetTrendChangesAsync([]));
    }

    [Fact]
    public async Task LatestReviewUsesTheIdToBreakTimestampTies()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var item = new WaitingTime { Name = "Record" };
        db.WaitingTimes.Add(item);
        await db.SaveChangesAsync();
        for (var index = 0; index < 100; index++)
        {
            var entry = History(item, new DateTime(2026, 1, 1), 5);
            entry.Action = HistoryAction.Reviewed;
            entry.Username = $"Reviewer {index}";
            db.WaitingTimeHistoryEntries.Add(entry);
        }
        await db.SaveChangesAsync();
        var reader = new WaitingTimeReadService(db, TimeProvider.System);
        var review = Assert.Single(await reader.GetLatestReviewsAsync([item.Id, item.Id]));
        Assert.Equal("Reviewer 99", review.Value.Username);
        Assert.Empty(await reader.GetLatestReviewsAsync([]));
    }

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
