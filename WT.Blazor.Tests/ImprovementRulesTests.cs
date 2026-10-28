using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class ImprovementRulesTests
{
    [Fact]
    public void ReviewReasonsRemainUntilMissingMetadataIsCompleted()
    {
        var item = new WaitingTime { EstimateStatus = EstimateStatus.Available };
        var now = new DateTime(2026, 10, 28);
        var reasons = EstimateReview.Reasons(item, 3, now);
        Assert.True(reasons.HasFlag(EstimateReviewReason.MissingVerification));
        Assert.True(reasons.HasFlag(EstimateReviewReason.MissingOwner));
        Assert.True(reasons.HasFlag(EstimateReviewReason.MissingEndpoint));
        item.VerifiedAt = now;
        reasons = EstimateReview.Reasons(item, 3, now);
        Assert.False(reasons.HasFlag(EstimateReviewReason.MissingVerification));
        Assert.Equal(2, EstimateReview.IndividualReasons(reasons).Count());
        item.ResponsibleDepartment = "Imaging";
        item.ReferralEndpoint = ReferralEndpoint.Investigation;
        Assert.Equal(EstimateReviewReason.None, EstimateReview.Reasons(item, 3, now));
        item.VerifiedAt = now.AddMonths(-4);
        Assert.Equal(EstimateReviewReason.Overdue, EstimateReview.Reasons(item, 3, now));
        Assert.True(EstimateReview.Priority(EstimateReviewReason.MissingVerification) > EstimateReview.Priority(EstimateReviewReason.Overdue));
    }

    [Theory]
    [InlineData(4, WaitUnit.Vikur, 40, WaitUnit.Vikur, true)]
    [InlineData(40, WaitUnit.Vikur, 4, WaitUnit.Vikur, true)]
    [InlineData(4, WaitUnit.Vikur, 7, WaitUnit.Vikur, false)]
    [InlineData(1, WaitUnit.Manadir, 30, WaitUnit.Dagar, false)]
    [InlineData(0, WaitUnit.Dagar, 28, WaitUnit.Dagar, true)]
    [InlineData(0, WaitUnit.Dagar, 27, WaitUnit.Dagar, false)]
    [InlineData(1, WaitUnit.Dagar, int.MaxValue, WaitUnit.Dagar, true)]
    public void LargeChangesUseCanonicalDaysAndBothThresholds(int previous, WaitUnit previousUnit, int next, WaitUnit nextUnit, bool expected)
    {
        var item = new WaitingTime { WaitValue = previous, Unit = previousUnit, ReferralEndpoint = ReferralEndpoint.Treatment };
        Assert.Equal(expected, EstimateChange.RequiresConfirmation(item, EstimateStatus.Available, item.ReferralEndpoint, next, nextUnit));
        Assert.False(EstimateChange.RequiresConfirmation(item, EstimateStatus.Unknown, item.ReferralEndpoint, next, nextUnit));
    }

    [Fact]
    public async Task UnconfirmedLargeChangeCannotMutateTheRecordOrAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance, new HttpContextAccessor(), TimeProvider.System);
        var item = await manager.CreateAsync(new WaitingTimeCreate("MRI", Hospital.Klaksvikar, null, 4, WaitUnit.Vikur, "Routine adults", "staff", ReferralEndpoint: ReferralEndpoint.Investigation, ComparisonCode: " mri-1 "));
        Assert.Equal("MRI-1", item.ComparisonCode);
        var update = new WaitingTimeUpdate(item.Id, item.Version, item.Name, item.Hospital, null, 40, item.Unit, item.Note, "staff",
            ReferralEndpoint: item.ReferralEndpoint, ComparisonCode: item.ComparisonCode);
        await Assert.ThrowsAsync<LargeEstimateChangeException>(() => manager.UpdateAsync(update));
        Assert.Equal(4, item.WaitValue);
        Assert.Equal(1, item.Version);
        Assert.Single(await db.WaitingTimeHistoryEntries.ToListAsync());
        await manager.UpdateAsync(update with { ConfirmLargeChange = true });
        Assert.Equal(40, item.WaitValue);
        Assert.Equal(2, await db.WaitingTimeHistoryEntries.CountAsync());
    }

    [Fact]
    public void ComparisonsRequireAnExplicitUnambiguousMatchAcrossHospitals()
    {
        var one = Item(1, Hospital.Klaksvikar);
        var two = Item(2, Hospital.Suduroyar);
        two.Name = "Different local service label";
        two.Unit = WaitUnit.Dagar;
        two.WaitValue = 28;
        var group = Assert.Single(WaitingTimeComparisons.Build([one, two]));
        Assert.Equal(2, group.Estimates.Count);
        Assert.Equal(28, WaitDuration.ToDays(group.Estimates[0].WaitValue, group.Estimates[0].Unit));
        Assert.Empty(WaitingTimeComparisons.Build([one, two, Item(3, Hospital.Klaksvikar)]));
        two.Note = "Routine children";
        Assert.Empty(WaitingTimeComparisons.Build([one, two]));
        two.Note = one.Note;
        two.ReferralEndpoint = ReferralEndpoint.Treatment;
        Assert.Empty(WaitingTimeComparisons.Build([one, two]));
        two.ReferralEndpoint = one.ReferralEndpoint;
        two.ComparisonCode = null;
        Assert.Empty(WaitingTimeComparisons.Build([one, two]));
        two.ComparisonCode = one.ComparisonCode;
        two.EstimateStatus = EstimateStatus.Unknown;
        Assert.Empty(WaitingTimeComparisons.Build([one, two]));
        two.EstimateStatus = EstimateStatus.Available;
        two.VerifiedAt = null;
        Assert.Empty(WaitingTimeComparisons.Build([one, two]));
        two.VerifiedAt = DateTime.UtcNow;
        one.Note = two.Note = "";
        Assert.Empty(WaitingTimeComparisons.Build([one, two]));
    }

    [Fact]
    public async Task GuidanceRequiresBothLanguagesAndPublicationIsAuditedAndReversible()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var notifier = new PortalSettingsChangeNotifier();
        var notifications = 0;
        notifier.Changed += (_, _) => notifications++;
        var service = new PortalSettingsService(db, notifier, TimeProvider.System);
        var settings = await service.GetAsync();
        settings.PublishPatientGuidance = true;
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(settings, "admin"));
        Assert.Empty(await db.PortalSettingsAuditEntries.ToListAsync());
        settings.ReferralInstructionsFo = "Føroysk ávísing";
        settings.ReferralInstructionsEn = "Approved referral instructions";
        settings.ContactGuidanceFo = "Samband";
        settings.ContactGuidanceEn = "Contact the referral office";
        await service.SaveAsync(settings, "admin");
        var saved = await service.GetAsync();
        Assert.True(saved.PublishPatientGuidance);
        var audit = Assert.Single(await service.GetRecentChangesAsync());
        Assert.False(audit.OldPublishPatientGuidance);
        Assert.True(audit.NewPublishPatientGuidance);
        Assert.Equal("Contact the referral office", audit.NewContactGuidanceEn);
        saved.PublishPatientGuidance = false;
        await service.SaveAsync(saved, "admin");
        Assert.False((await service.GetAsync()).PublishPatientGuidance);
        Assert.Equal(2, notifications);
    }

    private static WaitingTime Item(int id, Hospital hospital) => new()
    {
        Id = id,
        Name = "MRI",
        Hospital = hospital,
        WaitValue = 4,
        Unit = WaitUnit.Vikur,
        VerifiedAt = DateTime.UtcNow,
        ReferralEndpoint = ReferralEndpoint.Investigation,
        ComparisonCode = "MRI-1",
        Note = "Routine adults"
    };
}
