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

public class EstimatePublicationTests
{
    [Fact]
    public void ExpiryUsesVerificationAtTheExactCalendarBoundary()
    {
        var settings = new PortalSettings { EstimateExpiryMonths = 12 };
        var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var item = new WaitingTime { WaitValue = 6, Unit = WaitUnit.Vikur, VerifiedAt = now.AddMonths(-12).AddTicks(1) };
        Assert.False(ReferralEstimate.IsExpired(item, settings, now));
        item.VerifiedAt = now.AddMonths(-12);
        Assert.True(ReferralEstimate.IsExpired(item, settings, now));
        Assert.DoesNotContain("6 ", ReferralEstimateText.PublicWaitText(item, settings, now));
        item.UpdatedAt = now; // Editing metadata cannot make this current.
        Assert.True(ReferralEstimate.IsExpired(item, settings, now));
        item.VerifiedAt = null;
        Assert.True(ReferralEstimate.IsExpired(item, settings, now));
        item.VerifiedAt = now.AddDays(1);
        Assert.True(ReferralEstimate.IsExpired(item, settings, now));
        item.EstimateStatus = EstimateStatus.ServiceUnavailable;
        Assert.False(ReferralEstimate.IsExpired(item, settings, now));
    }

    [Fact]
    public async Task RangeBoundsAreValidatedAuditedAndNeverPlottedAsSingleValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance, new HttpContextAccessor(), TimeProvider.System);
        var create = new WaitingTimeCreate
        {
            Name = "Range",
            Hospital = Hospital.Klaksvikar,
            Department = null,
            WaitValue = 6,
            Unit = WaitUnit.Vikur,
            Note = "Adults",
            Username = "staff",
            ReferralEndpoint = ReferralEndpoint.Treatment,
            WaitUpperValue = 10
        };
        var item = await manager.CreateAsync(create);
        Assert.Contains("6–10", ReferralEstimateText.WaitText(item));
        Assert.Contains("42–70", ReferralEstimateText.NumericText(item, canonicalDays: true));
        var reader = new WaitingTimeReadService(db, TimeProvider.System);
        Assert.Empty(await reader.GetTrendPointsAsync(item.Id));
        Assert.Empty(await reader.GetTrendChangesAsync([item.Id]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.CreateAsync(create with { WaitUpperValue = 5 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.CreateAsync(create with { WaitUpperValue = int.MaxValue }));
        var update = new WaitingTimeUpdate
        {
            Id = item.Id,
            ExpectedVersion = item.Version,
            Name = item.Name,
            Hospital = item.Hospital,
            Department = null,
            WaitValue = 6,
            Unit = item.Unit,
            Note = item.Note,
            Username = "staff",
            ReferralEndpoint = item.ReferralEndpoint,
            WaitUpperValue = 20
        };
        await Assert.ThrowsAsync<LargeEstimateChangeException>(() => manager.UpdateAsync(update));
        Assert.Equal(10, item.WaitUpperValue);
        item = await manager.UpdateAsync(update with { ConfirmLargeChange = true });
        var audit = await db.WaitingTimeHistoryEntries.OrderByDescending(entry => entry.Id).FirstAsync();
        Assert.Equal(10, audit.OldWaitUpperValue);
        Assert.Equal(20, audit.NewWaitUpperValue);
        Assert.Contains("6–20", HistoryExportRow.From(audit).NewWait);
        var previousSeries = item.EstimateSeries;
        item = await manager.UpdateAsync(update with { ExpectedVersion = item.Version, WaitUpperValue = null, ConfirmLargeChange = true });
        Assert.True(item.EstimateSeries > previousSeries);
        Assert.Single(await reader.GetTrendPointsAsync(item.Id));
        Assert.Empty(await reader.GetTrendChangesAsync([item.Id]));
    }

    [Fact]
    public async Task DirectTrendReportsUseTheSameExpiryPolicyAndRangeText()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var settings = new PortalSettingsService(db, new PortalSettingsChangeNotifier(), TimeProvider.System);
        await settings.SaveAsync(new PortalSettings { EstimateExpiryMonths = 1 }, "admin");
        var item = new WaitingTime { Name = "Direct report", Hospital = Hospital.Klaksvikar, ResponsibleDepartment = "Imaging", ReferralEndpoint = ReferralEndpoint.Investigation, WaitValue = 6, WaitUpperValue = 10, Unit = WaitUnit.Vikur, VerifiedAt = DateTime.UtcNow.AddMonths(-2) };
        db.WaitingTimes.Add(item);
        await db.SaveChangesAsync();
        var service = new TrendReportService(new WaitingTimeReadService(db, TimeProvider.System), settings, TimeProvider.System);
        var expired = await service.GetAsync(item.Id);
        Assert.NotNull(expired);
        Assert.True(expired.IsExpired);
        Assert.True(EstimateReview.Reasons(item, 1, DateTime.UtcNow).HasFlag(EstimateReviewReason.Overdue));
        Assert.Equal(0, (await new WaitingTimeReadService(db, TimeProvider.System).GetOperationsSummaryAsync(30, 3, 12)).NeedsReview);
        Assert.Equal(1, (await new WaitingTimeReadService(db, TimeProvider.System).GetOperationsSummaryAsync(30, 3, 12, estimateExpiryMonths: 1)).NeedsReview);
        Assert.DoesNotContain("6–10", expired.CurrentWait);
        item.VerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var current = await service.GetAsync(item.Id);
        Assert.NotNull(current);
        Assert.False(current.IsExpired);
        Assert.True(current.IsRange);
        Assert.Contains("6–10", current.CurrentWait);
        Assert.Empty(current.Points);
    }

    [Fact]
    public async Task UpgradePreservesExistingValuesAndAddsTwelveMonthExpiryWithoutInventingRanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261028172501_AddComparisonsAndPatientGuidance");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO PortalSettings (Id, GreenMaximumAgeMonths, YellowMaximumAgeMonths, LongWaitMonths, Version, PublishPatientGuidance) VALUES (1, 3, 6, 12, 1, 0)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO WaitingTimes (Name, Hospital, WaitValue, Unit, UpdatedAt, IsActive, Version, EstimateStatus, ReferralEndpoint, EstimateSeries) VALUES ('Existing', 2, 6, 1, '2026-10-01', 1, 1, 0, 0, 1)");
        await migrator.MigrateAsync();
        Assert.Equal(12, (await db.PortalSettings.SingleAsync()).EstimateExpiryMonths);
        var item = await db.WaitingTimes.SingleAsync();
        Assert.Equal(6, item.WaitValue);
        Assert.Null(item.WaitUpperValue);
        Assert.Null(item.VerifiedAt);
    }
}
