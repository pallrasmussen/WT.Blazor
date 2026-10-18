using Microsoft.EntityFrameworkCore;
using WT.Blazor.Models;

namespace WT.Blazor.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await db.Database.MigrateAsync();

        if (!await db.PortalSettings.AnyAsync())
        {
            db.PortalSettings.Add(new PortalSettings());
            await db.SaveChangesAsync();
        }

        if (!await db.WaitingTimes.AnyAsync())
        {
            SeedWaitingTimes(db, now);
            await db.SaveChangesAsync();
        }

        await SeedDemoTrendHistoryAsync(db, now);
    }

    // Gives every seeded item a few months of synthetic history so the "Sí gongdina" trend
    // graph has something to show out of the box, instead of "not enough data" for everything.
    // Safe to run on every startup: skips items that already have real update history.
    private static async Task SeedDemoTrendHistoryAsync(ApplicationDbContext db, DateTime now)
    {
        var itemsWithoutHistory = await db.WaitingTimes
            .Where(w => w.IsActive && !db.WaitingTimeHistoryEntries.Any(h => h.WaitingTimeId == w.Id && h.Action == HistoryAction.Updated))
            .ToListAsync();

        if (itemsWithoutHistory.Count == 0) return;

        var random = new Random(1234);
        foreach (var item in itemsWithoutHistory)
        {
            var monthsBack = new[] { 5, 4, 3, 2, 1 };
            var previousValue = item.WaitValue;
            foreach (var months in monthsBack)
            {
                var delta = random.Next(-2, 3);
                previousValue = Math.Max(1, previousValue - delta);
                db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry
                {
                    WaitingTimeId = item.Id,
                    Timestamp = now.AddMonths(-months),
                    Hospital = item.Hospital,
                    Department = item.Department,
                    Name = item.Name,
                    Action = HistoryAction.Updated,
                    Detail = "Demo",
                    OldWaitValue = null,
                    OldUnit = null,
                    NewWaitValue = previousValue,
                    NewUnit = item.Unit,
                    NewWaitDays = WaitDuration.ToDays(previousValue, item.Unit),
                    Username = "demo"
                });
            }

            // Final point matches the item's actual current value, at the current time.
            db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry
            {
                WaitingTimeId = item.Id,
                Timestamp = now,
                Hospital = item.Hospital,
                Department = item.Department,
                Name = item.Name,
                Action = HistoryAction.Updated,
                Detail = "Demo",
                NewWaitValue = item.WaitValue,
                NewUnit = item.Unit,
                NewWaitDays = WaitDuration.ToDays(item.WaitValue, item.Unit),
                Username = "demo"
            });
        }

        await db.SaveChangesAsync();
    }

    private static void SeedWaitingTimes(ApplicationDbContext db, DateTime now)
    {
        var seed = new[]
        {
            new WaitingTime { Name = "Ambulant psykiatri", Hospital = Hospital.Landssjukrahusid, Department = Department.Psykiatriskidepilin, WaitValue = 2, Unit = WaitUnit.Manadir, UpdatedAt = now },
            new WaitingTime { Name = "Dermatologi", Hospital = Hospital.Landssjukrahusid, Department = Department.Medisinskidepilin, WaitValue = 14, Unit = WaitUnit.Manadir, UpdatedAt = now },
            new WaitingTime { Name = "Endoskopi", Hospital = Hospital.Klaksvikar, WaitValue = 2, Unit = WaitUnit.Manadir, UpdatedAt = now },
            new WaitingTime { Name = "Endoskopi", Hospital = Hospital.Suduroyar, WaitValue = 3, Unit = WaitUnit.Manadir, UpdatedAt = now },
            new WaitingTime { Name = "Eygnadeildin, vaksin", Hospital = Hospital.Landssjukrahusid, Department = Department.Skurddepilin, WaitValue = 15, Unit = WaitUnit.Manadir, UpdatedAt = now },
            new WaitingTime { Name = "Gastroenterologi", Hospital = Hospital.Landssjukrahusid, Department = Department.Medisinskidepilin, WaitValue = 6, Unit = WaitUnit.Manadir, UpdatedAt = now },
            new WaitingTime { Name = "Kardiologi", Hospital = Hospital.Landssjukrahusid, Department = Department.Medisinskidepilin, WaitValue = 6, Unit = WaitUnit.Manadir, UpdatedAt = now },
        };
        db.WaitingTimes.AddRange(seed);
    }
}
