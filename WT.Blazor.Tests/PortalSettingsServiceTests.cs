using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class PortalSettingsServiceTests
{
    [Fact]
    public async Task SaveAsyncPersistsValidSettings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new PortalSettingsService(db, new PortalSettingsChangeNotifier(), TimeProvider.System);

        await service.SaveAsync(new PortalSettings
        {
            GreenMaximumAgeMonths = 2,
            YellowMaximumAgeMonths = 5,
            LongWaitMonths = 10
        }, "test-user");

        var settings = await service.GetAsync();

        Assert.Equal(2, settings.GreenMaximumAgeMonths);
        Assert.Equal(5, settings.YellowMaximumAgeMonths);
        Assert.Equal(10, settings.LongWaitMonths);
        var auditEntry = Assert.Single(await service.GetRecentChangesAsync());
        Assert.Equal("test-user", auditEntry.Username);
        Assert.Equal(PortalSettings.DefaultGreenMaximumAgeMonths, auditEntry.OldGreenMaximumAgeMonths);
        Assert.Equal(2, auditEntry.NewGreenMaximumAgeMonths);
    }

    [Fact]
    public async Task SaveAsyncRejectsYellowThresholdAtOrBeforeGreenThreshold()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new PortalSettingsService(db, new PortalSettingsChangeNotifier(), TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveAsync(new PortalSettings
        {
            GreenMaximumAgeMonths = 6,
            YellowMaximumAgeMonths = 6
        }, "test-user"));
    }
}