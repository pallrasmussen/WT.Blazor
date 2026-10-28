using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class DurationManagementTests
{
    [Theory]
    [InlineData(WaitUnit.Vikur)]
    [InlineData(WaitUnit.Manadir)]
    public async Task OverflowingCreateAndUpdateCannotWriteRecordsOrHistory(WaitUnit unit)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance, new HttpContextAccessor(), TimeProvider.System);
        var maximum = WaitDuration.MaximumValue(unit);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.CreateAsync(new WaitingTimeCreate
        {
            Name = "Invalid",
            Hospital = Hospital.Klaksvikar,
            Department = null,
            WaitValue = maximum + 1,
            Unit = unit,
            Note = null,
            Username = "tester"
        }));
        Assert.Empty(await db.WaitingTimes.ToListAsync());
        Assert.Empty(await db.WaitingTimeHistoryEntries.ToListAsync());
        var item = await manager.CreateAsync(new WaitingTimeCreate
        {
            Name = "Valid",
            Hospital = Hospital.Klaksvikar,
            Department = null,
            WaitValue = maximum,
            Unit = unit,
            Note = null,
            Username = "tester"
        });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.UpdateAsync(new WaitingTimeUpdate
        {
            Id = item.Id,
            ExpectedVersion = item.Version,
            Name = item.Name,
            Hospital = item.Hospital,
            Department = null,
            WaitValue = maximum + 1,
            Unit = unit,
            Note = null,
            Username = "tester"
        }));
        Assert.Equal(maximum, (await db.WaitingTimes.AsNoTracking().SingleAsync()).WaitValue);
        var history = Assert.Single(await db.WaitingTimeHistoryEntries.ToListAsync());
        Assert.Equal(WaitDuration.ToDays(maximum, unit), history.NewWaitDays);
    }
}
