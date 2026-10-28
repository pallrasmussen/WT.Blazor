using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Components.Pages;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class WaitingTimeValidationTests
{
    [Fact]
    public void NormalizationIsSharedWithoutChangingTheUsersDraft()
    {
        var form = new WaitingTimeFormState
        {
            Name = "  MRI  ",
            HospitalName = DisplayNames.For(Hospital.Klaksvikar),
            DepartmentName = DisplayNames.For(Department.Medisinskidepilin),
            EstimateStatus = EstimateStatus.Available,
            WaitValue = 6,
            WaitUpperValue = 10,
            Unit = WaitUnit.Vikur,
            ComparisonCode = " mri-1 ",
            ResponsibleDepartment = " Imaging ",
            Note = "  Adults  "
        };
        var input = form.ToInput();
        var normalized = WaitingTimeValidation.NormalizeAndValidate(input);
        Assert.Equal("MRI", normalized.Name);
        Assert.Equal("MRI-1", normalized.ComparisonCode);
        Assert.Equal("Imaging", normalized.ResponsibleDepartment);
        Assert.Equal("Adults", normalized.Note);
        Assert.Null(normalized.Department);
        Assert.Equal(10, normalized.WaitUpperValue);
        Assert.Equal("  MRI  ", form.Name);
        var unavailable = WaitingTimeValidation.Normalize(input with { EstimateStatus = EstimateStatus.Unknown });
        Assert.Equal(0, unavailable.WaitValue);
        Assert.Null(unavailable.WaitUpperValue);
    }

    [Theory]
    [InlineData(-1, null, WaitingTimeInputError.NegativeWait)]
    [InlineData(6, 5, WaitingTimeInputError.InvalidUpperBound)]
    [InlineData(6, int.MaxValue, WaitingTimeInputError.InvalidUpperBound)]
    public async Task FormsAndManagementUseTheSameDurationValidation(int lower, int? upper, WaitingTimeInputError expected)
    {
        var form = new WaitingTimeFormState
        {
            Name = "MRI",
            HospitalName = DisplayNames.For(Hospital.Klaksvikar),
            EstimateStatus = EstimateStatus.Available,
            Unit = WaitUnit.Vikur,
            WaitValue = lower,
            WaitUpperValue = upper
        };
        var input = form.ToInput();
        Assert.Equal(expected, WaitingTimeValidation.Validate(input)?.Error);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance, new HttpContextAccessor(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.CreateAsync(new WaitingTimeCreate(input) { Username = "staff" }));
        Assert.Empty(await db.WaitingTimes.ToListAsync());
        Assert.Empty(await db.WaitingTimeHistoryEntries.ToListAsync());
    }

    [Fact]
    public async Task ManagementReturnsDetachedPersistedValuesAndNoOpDoesNotAdvanceVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new FixedClock();
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance, new HttpContextAccessor(), clock);
        var item = await manager.CreateAsync(new WaitingTimeCreate
        {
            Name = "MRI",
            Hospital = Hospital.Klaksvikar,
            WaitValue = 6,
            Unit = WaitUnit.Vikur,
            Username = "staff"
        });
        var update = new WaitingTimeUpdate(WaitingTimeInput.From(item)) { Id = item.Id, ExpectedVersion = item.Version, Username = "staff" };
        var noOp = await manager.UpdateAsync(update);
        Assert.Equal(1, noOp.Version);
        Assert.Single(await db.WaitingTimeHistoryEntries.ToListAsync());
        var saved = await manager.UpdateAsync(update with { WaitValue = 7, Name = " MRI edited " });
        Assert.Equal("MRI edited", saved.Name);
        Assert.Equal(2, saved.Version);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, saved.VerifiedAt);
        Assert.NotSame(item, saved);
        Assert.Equal(1, item.Version);
        Assert.Equal(6, item.WaitValue);
        saved.Name = "UI-only mutation";
        Assert.Equal("MRI edited", (await db.WaitingTimes.AsNoTracking().SingleAsync()).Name);
        var reviewed = await manager.MarkReviewedAsync(item.Id, 2, "reviewer");
        Assert.Equal(3, reviewed.Version);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, reviewed.UpdatedAt);
        var deactivated = await manager.DeactivateAsync(item.Id, 3, "admin");
        Assert.Equal(4, deactivated.Version);
        Assert.False(deactivated.IsActive);
        Assert.True(reviewed.IsActive);
        Assert.Equal(2, saved.Version);
        var reactivated = await manager.ReactivateAsync(item.Id, 4, "admin");
        Assert.True(reactivated.IsActive);
        Assert.Equal(5, reactivated.Version);
        Assert.False(deactivated.IsActive);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);
    }
}
