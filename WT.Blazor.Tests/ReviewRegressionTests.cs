using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class ReviewRegressionTests
{
    [Fact]
    public async Task ServicesSharingAContextSerializeOverlappingOperations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var blocker = new BlockingReader();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection)
            .AddInterceptors(blocker).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        blocker.Enabled = true;
        var read = new WaitingTimeReadService(db, TimeProvider.System).GetActiveAsync(null, null);
        await blocker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var settings = new PortalSettingsService(db, new PortalSettingsChangeNotifier(), TimeProvider.System).GetAsync();
        try
        {
            Assert.False(settings.IsCompleted);
        }
        finally
        {
            blocker.Release.TrySetResult();
        }
        await Task.WhenAll(read, settings).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class BlockingReader : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                Enabled = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task DefaultInitializationDoesNotSeedWaitingTimesOrInventHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await SeedData.InitializeAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.WaitingTimes.ToListAsync());
        Assert.Single(await db.PortalSettings.ToListAsync());
        db.WaitingTimes.Add(new WaitingTime { Name = "Real record", Hospital = Hospital.Klaksvikar });
        await db.SaveChangesAsync();

        await SeedData.InitializeAsync(scope.ServiceProvider);

        Assert.Empty(await db.WaitingTimeHistoryEntries.ToListAsync());
        Assert.Single(await db.WaitingTimes.ToListAsync());
    }

    [Theory]
    [InlineData("update")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    [InlineData("review")]
    public async Task SaveTimeConflictIsTranslatedAndFailedAuditIsDiscarded(string action)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var item = new WaitingTime { Name = "Original", Hospital = Hospital.Klaksvikar };
        db.WaitingTimes.Add(item);
        await db.SaveChangesAsync();
        // Emulate a second writer committing after this context loaded the row.
        await using (var other = new ApplicationDbContext(options))
            await other.WaitingTimes.ExecuteUpdateAsync(update => update.SetProperty(row => row.Version, 2));
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance,
            new HttpContextAccessor(), TimeProvider.System);

        Task Change() => action switch
        {
            "update" => manager.UpdateAsync(new WaitingTimeUpdate(item.Id, 1, "Changed", Hospital.Klaksvikar, null, 3, WaitUnit.Dagar, null, "tester")),
            "deactivate" => manager.DeactivateAsync(item.Id, 1, "tester"),
            "reactivate" => manager.ReactivateAsync(item.Id, 1, "tester"),
            _ => manager.MarkReviewedAsync(item.Id, 1, "tester")
        };

        var exception = await Assert.ThrowsAsync<WaitingTimeConcurrencyException>(Change);
        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await db.WaitingTimeHistoryEntries.ToListAsync());
        await manager.MarkReviewedAsync(item.Id, 2, "tester");
        Assert.Single(await db.WaitingTimeHistoryEntries.ToListAsync());
    }

    [Fact]
    public async Task PublicReportsExcludeInactiveRecordsAndTrendIndicatorsUseEndpoints()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var item = new WaitingTime { Name = "Record", Hospital = Hospital.Klaksvikar, IsActive = false };
        db.WaitingTimes.Add(item);
        await db.SaveChangesAsync();
        for (var index = 0; index < 100; index++)
            db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry
            {
                WaitingTimeId = item.Id,
                Name = item.Name,
                Action = HistoryAction.Updated,
                Timestamp = new DateTime(2026, 1, 1).AddDays(index),
                NewWaitValue = index + 1,
                NewUnit = WaitUnit.Vikur
            });
        await db.SaveChangesAsync();
        var reader = new WaitingTimeReadService(db, TimeProvider.System);
        Assert.Null(await new TrendReportService(reader, new WT.Blazor.Tests.TestInfrastructure.DefaultSettingsService(), TimeProvider.System).GetAsync(item.Id));
        var changes = await reader.GetTrendChangesAsync([item.Id]);
        Assert.Equal(7, changes[item.Id]);
    }
}
