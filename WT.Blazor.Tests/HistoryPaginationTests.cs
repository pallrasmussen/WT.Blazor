using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HistoryPaginationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("name", false)]
    [InlineData("name", true)]
    [InlineData("action", false)]
    [InlineData("action", true)]
    [InlineData("username", false)]
    [InlineData("username", true)]
    public async Task EqualSortValuesHaveStablePageBoundaries(string? sortBy, bool descending)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var item = new WaitingTime { Name = "Record", Hospital = Hospital.Klaksvikar };
        db.WaitingTimes.Add(item);
        await db.SaveChangesAsync();
        for (var index = 0; index < 7; index++)
            db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry
            {
                WaitingTimeId = item.Id,
                Name = item.Name,
                Username = "same-user",
                Action = HistoryAction.Updated,
                Timestamp = new DateTime(2026, 10, 1)
            });
        await db.SaveChangesAsync();
        var reader = new WaitingTimeReadService(db, TimeProvider.System);
        var ids = new List<int>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await reader.GetHistoryPageAsync(new HistoryQuery(SortBy: sortBy, Descending: descending, PageNumber: page, PageSize: 2));
            Assert.Equal(7, result.TotalCount);
            ids.AddRange(result.Entries.Select(entry => entry.Id));
        }
        var expected = Enumerable.Range(1, 7);
        if (sortBy is not null || descending) expected = expected.Reverse();
        Assert.Equal(expected, ids);
        Assert.Equal(7, ids.Distinct().Count());
    }
}
