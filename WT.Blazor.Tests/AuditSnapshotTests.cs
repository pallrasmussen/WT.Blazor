using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class AuditSnapshotTests
{
    [Fact]
    public async Task RangeAndAvailabilitySnapshotsSurviveEveryLifecycleActionAndUnitConversion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { TraceIdentifier = "snapshot-request" } }, TimeProvider.System);
        var item = await manager.CreateAsync(new WaitingTimeCreate
        {
            Name = "MRI",
            Hospital = Hospital.Klaksvikar,
            WaitValue = 6,
            WaitUpperValue = 10,
            Unit = WaitUnit.Vikur,
            ResponsibleDepartment = "Imaging",
            ComparisonCode = "MRI-1",
            ReferralEndpoint = ReferralEndpoint.Investigation,
            Username = "creator"
        });
        item = await manager.UpdateAsync(new WaitingTimeUpdate(WaitingTimeInput.From(item))
        {
            Id = item.Id,
            ExpectedVersion = item.Version,
            WaitValue = 42,
            WaitUpperValue = 70,
            Unit = WaitUnit.Dagar,
            Username = "editor"
        });
        item = await manager.MarkReviewedAsync(item.Id, item.Version, "reviewer");
        item = await manager.DeactivateAsync(item.Id, item.Version, "admin", "Fixture");
        item = await manager.ReactivateAsync(item.Id, item.Version, "admin");
        item = await manager.UpdateAsync(new WaitingTimeUpdate(WaitingTimeInput.From(item))
        {
            Id = item.Id,
            ExpectedVersion = item.Version,
            EstimateStatus = EstimateStatus.Unknown,
            Username = "editor"
        });
        await manager.UpdateAsync(new WaitingTimeUpdate(WaitingTimeInput.From(item))
        {
            Id = item.Id,
            ExpectedVersion = item.Version,
            EstimateStatus = EstimateStatus.Available,
            WaitValue = 0,
            Username = "editor"
        });
        var entries = await db.WaitingTimeHistoryEntries.AsNoTracking().OrderBy(entry => entry.Id).ToListAsync();
        Assert.Equal(7, entries.Count);
        var created = entries[0];
        Assert.Null(created.OldWaitValue);
        Assert.Equal((6, 10, WaitUnit.Vikur, 42), (created.NewWaitValue, created.NewWaitUpperValue, created.NewUnit, created.NewWaitDays));
        Assert.Equal("Imaging", created.ResponsibleDepartment);
        Assert.Equal("MRI-1", created.ComparisonCode);
        Assert.Equal(ReferralEndpoint.Investigation, created.ReferralEndpoint);
        Assert.Equal("creator", created.Username);
        Assert.All(entries, entry => Assert.Equal("snapshot-request", entry.RequestId));
        var conversion = entries[1];
        Assert.Equal((6, 10, WaitUnit.Vikur, 42), (conversion.OldWaitValue, conversion.OldWaitUpperValue, conversion.OldUnit, conversion.OldWaitDays));
        Assert.Equal((42, 70, WaitUnit.Dagar, 42), (conversion.NewWaitValue, conversion.NewWaitUpperValue, conversion.NewUnit, conversion.NewWaitDays));
        foreach (var entry in new[] { entries[2], entries[4] })
        {
            Assert.Equal((42, 70, WaitUnit.Dagar, 42), (entry.OldWaitValue, entry.OldWaitUpperValue, entry.OldUnit, entry.OldWaitDays));
            Assert.Equal((42, 70, WaitUnit.Dagar, 42), (entry.NewWaitValue, entry.NewWaitUpperValue, entry.NewUnit, entry.NewWaitDays));
        }
        var deactivated = entries[3];
        Assert.Equal(HistoryAction.Deactivated, deactivated.Action);
        Assert.Equal(70, deactivated.OldWaitUpperValue);
        Assert.Null(deactivated.NewWaitValue);
        Assert.Null(deactivated.NewUnit);
        Assert.Null(deactivated.NewWaitDays);
        Assert.Equal(70, deactivated.NewWaitUpperValue); // Existing snapshot metadata is preserved.
        var unknown = entries[5];
        Assert.Equal(42, unknown.OldWaitValue);
        Assert.Equal(70, unknown.OldWaitUpperValue);
        Assert.Null(unknown.NewWaitValue);
        Assert.Null(unknown.NewWaitUpperValue);
        Assert.Null(unknown.NewWaitDays);
        var zero = entries[6];
        Assert.Null(zero.OldWaitValue);
        Assert.Null(zero.OldWaitUpperValue);
        Assert.Equal(0, zero.NewWaitValue);
        Assert.Equal(0, zero.NewWaitDays);
        Assert.Equal(1, created.EstimateSeries);
        Assert.Equal(3, zero.EstimateSeries);
    }
}
