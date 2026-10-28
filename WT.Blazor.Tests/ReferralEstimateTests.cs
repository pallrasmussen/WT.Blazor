using WT.Blazor.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class ReferralEstimateTests
{
    [Theory]
    [InlineData(EstimateStatus.Unknown)]
    [InlineData(EstimateStatus.ServiceUnavailable)]
    public async Task UnavailableEstimatesAreNeverZeroPointsAndResumingStartsANewSeries(EstimateStatus status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new Clock();
        var manager = Manager(db, clock);
        var item = await manager.CreateAsync(new WaitingTimeCreate
        {
            Name = "MRI",
            Hospital = Hospital.Klaksvikar,
            Department = null,
            WaitValue = 4,
            Unit = WaitUnit.Vikur,
            Note = "Adults",
            Username = "staff",
            ReferralEndpoint = ReferralEndpoint.Investigation,
            ResponsibleDepartment = " Radiology "
        });
        var initialVerifiedAt = item.VerifiedAt;
        clock.Now = clock.Now.AddDays(1);
        item = await manager.UpdateAsync(Update(item, EstimateStatus.Available, 6));
        clock.Now = clock.Now.AddDays(1);
        item = await manager.UpdateAsync(Update(item, status, 999));
        Assert.Null(item.VerifiedAt);
        var audit = await db.WaitingTimeHistoryEntries.OrderByDescending(entry => entry.Id).FirstAsync();
        Assert.Equal(status, audit.EstimateStatus);
        Assert.Null(audit.NewWaitValue);
        Assert.Null(audit.NewWaitDays);
        Assert.Equal("Radiology", audit.ResponsibleDepartment);
        var reader = new TrendReadService(db, clock);
        Assert.Empty(await reader.GetTrendPointsAsync(item.Id));
        Assert.Empty(await reader.GetTrendChangesAsync([item.Id]));
        var unavailableReport = await new TrendReportService(reader, new WT.Blazor.Tests.TestInfrastructure.DefaultSettingsService(), TimeProvider.System).GetAsync(item.Id);
        Assert.NotNull(unavailableReport);
        Assert.Equal(ReferralEstimateText.StatusLabel(status), unavailableReport.CurrentWait);
        Assert.Null(unavailableReport.VerifiedAt);
        Assert.DoesNotContain("0 ", unavailableReport.CurrentWait);

        clock.Now = clock.Now.AddDays(1);
        item = await manager.UpdateAsync(Update(item, EstimateStatus.Available, 0));
        Assert.True(item.VerifiedAt > initialVerifiedAt);
        Assert.Equal(clock.Now.UtcDateTime, item.VerifiedAt);
        var point = Assert.Single(await reader.GetTrendPointsAsync(item.Id));
        Assert.Equal(0, point.Value); // A genuine available zero is valid.
        Assert.Empty(await reader.GetTrendChangesAsync([item.Id]));
        Assert.Equal(4, await db.WaitingTimeHistoryEntries.CountAsync());
    }

    [Fact]
    public async Task MetadataChangesPreserveVerificationAndEndpointChangesSeparateTrends()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new Clock();
        var manager = Manager(db, clock);
        var item = await manager.CreateAsync(new WaitingTimeCreate
        {
            Name = "MRI",
            Hospital = Hospital.Klaksvikar,
            Department = null,
            WaitValue = 4,
            Unit = WaitUnit.Vikur,
            Note = null,
            Username = "staff",
            ReferralEndpoint = ReferralEndpoint.Investigation,
            ResponsibleDepartment = "Radiology"
        });
        var verified = item.VerifiedAt;
        clock.Now = clock.Now.AddDays(10);
        item = await manager.UpdateAsync(Update(item, EstimateStatus.Available, 4) with { Note = "Adults only", ResponsibleDepartment = "Imaging" });
        Assert.Equal(verified, item.VerifiedAt);
        Assert.Equal(1, item.EstimateSeries);
        Assert.Equal(HistoryAction.Edited, (await db.WaitingTimeHistoryEntries.OrderByDescending(entry => entry.Id).FirstAsync()).Action);
        item = await manager.UpdateAsync(Update(item, EstimateStatus.Available, 4) with { ReferralEndpoint = ReferralEndpoint.Treatment });
        Assert.Equal(verified, item.VerifiedAt);
        Assert.Equal(2, item.EstimateSeries);
        var reader = new TrendReadService(db, clock);
        Assert.Single(await reader.GetTrendPointsAsync(item.Id));
        item = await manager.MarkReviewedAsync(item.Id, item.Version, "reviewer");
        Assert.Equal(clock.Now.UtcDateTime, item.VerifiedAt);
        Assert.Equal(4, await db.WaitingTimeHistoryEntries.CountAsync());
    }

    [Fact]
    public async Task MigrationPreservesLegacyValuesAndBackfillsOnlyEvidenceOfVerification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018181137_AddTrendHistoryLookupIndex");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO WaitingTimes (Id, Name, Hospital, WaitValue, Unit, UpdatedAt, IsActive, Version)
            VALUES (1, 'Legacy', 2, 7, 0, '2026-02-01 00:00:00', 1, 1),
                   (2, 'No history', 2, 0, 0, '2026-02-01 00:00:00', 1, 1);
            INSERT INTO WaitingTimeHistoryEntries
                (WaitingTimeId, Timestamp, RequestId, Hospital, Name, Action, NewWaitValue, NewUnit)
            VALUES (1, '2026-01-01 00:00:00', 'created', 2, 'Legacy', 0, 7, 0),
                   (1, '2026-02-01 00:00:00', 'text-edit', 2, 'Legacy', 2, 7, 0);
            """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO WaitingTimeHistoryEntries
                (WaitingTimeId, Timestamp, RequestId, Hospital, Name, Action, NewWaitValue, NewUnit, Username)
            VALUES (2, '2026-03-01 00:00:00', 'demo', 2, 'No history', 1, 3, 0, 'demo');
            """);
        await migrator.MigrateAsync();
        var legacy = await db.WaitingTimes.SingleAsync(item => item.Id == 1);
        Assert.Equal(7, legacy.WaitValue);
        Assert.Equal(ReferralEndpoint.Unspecified, legacy.ReferralEndpoint);
        Assert.Equal(EstimateStatus.Available, legacy.EstimateStatus);
        Assert.Null(legacy.ResponsibleDepartment);
        Assert.Equal(new DateTime(2026, 1, 1), legacy.VerifiedAt);
        Assert.Null((await db.WaitingTimes.SingleAsync(item => item.Id == 2)).VerifiedAt);
        Assert.Equal(3, await db.WaitingTimeHistoryEntries.CountAsync());
        Assert.All(await db.WaitingTimeHistoryEntries.ToListAsync(), entry => Assert.Equal(1, entry.EstimateSeries));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE WaitingTimeHistoryEntries SET Name='Changed' WHERE Id=1"));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidEstimateDefinitionsCannotWriteData(bool invalidStatus)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var create = new WaitingTimeCreate
        {
            Name = "MRI",
            Hospital = Hospital.Klaksvikar,
            Department = null,
            WaitValue = 4,
            Unit = WaitUnit.Vikur,
            Note = null,
            Username = "staff",
            EstimateStatus = invalidStatus ? (EstimateStatus)99 : EstimateStatus.Available,
            ReferralEndpoint = invalidStatus ? ReferralEndpoint.Investigation : (ReferralEndpoint)99
        };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Manager(db, new Clock()).CreateAsync(create));
        Assert.Empty(await db.WaitingTimes.ToListAsync());
        Assert.Empty(await db.WaitingTimeHistoryEntries.ToListAsync());
    }

    private static WaitingTimeUpdate Update(WaitingTime item, EstimateStatus status, int value) => new WaitingTimeUpdate
    {
        Id = item.Id,
        ExpectedVersion = item.Version,
        Name = item.Name,
        Hospital = item.Hospital,
        Department = item.Department,
        WaitValue = value,
        Unit = item.Unit,
        Note = item.Note,
        Username = "staff",
        EstimateStatus = status,
        ReferralEndpoint = item.ReferralEndpoint,
        ResponsibleDepartment = item.ResponsibleDepartment
    };

    private static WaitingTimeManagementService Manager(ApplicationDbContext db, TimeProvider clock) =>
        new(db, NullLogger<WaitingTimeManagementService>.Instance, new HttpContextAccessor(), clock);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
