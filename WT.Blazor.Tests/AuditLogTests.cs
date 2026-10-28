using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class AuditLogTests
{
    [Theory]
    [InlineData("metadata")]
    [InlineData("reactivate")]
    [InlineData("wait")]
    [InlineData("review")]
    public async Task OnlyWaitChangesAndExplicitReviewsRefreshExistingEstimates(string action)
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using var db = await CreateMigratedContextAsync(databasePath);
            var old = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var now = old.AddYears(1);
            var manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance,
                new HttpContextAccessor(), new FixedTimeProvider(old));
            var item = await manager.CreateAsync(new WaitingTimeCreate("Original", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "creator"));
            if (action == "reactivate") await manager.DeactivateAsync(item.Id, item.Version, "tester");
            manager = new WaitingTimeManagementService(db, NullLogger<WaitingTimeManagementService>.Instance,
                new HttpContextAccessor(), new FixedTimeProvider(now));
            if (action == "reactivate") await manager.ReactivateAsync(item.Id, item.Version, "tester");
            else if (action == "review") await manager.MarkReviewedAsync(item.Id, item.Version, "tester");
            else await manager.UpdateAsync(new WaitingTimeUpdate(item.Id, item.Version, "Renamed", item.Hospital, null,
                action == "wait" ? 4 : 3, item.Unit, "New note", "tester"));

            db.ChangeTracker.Clear();
            var saved = await db.WaitingTimes.SingleAsync();
            Assert.Equal(action is "wait" or "review" ? now.UtcDateTime : old.UtcDateTime, saved.UpdatedAt);
            Assert.Equal(action is "metadata" or "reactivate", UpdatedStatus.RequiresReview(saved.UpdatedAt, now.UtcDateTime));
            var latest = await db.WaitingTimeHistoryEntries.OrderByDescending(entry => entry.Id).FirstAsync();
            Assert.Equal(now.UtcDateTime, latest.Timestamp);
            Assert.Equal("tester", latest.Username);
        }
        finally { DeleteDatabase(databasePath); }
    }

    [Fact]
    public async Task MarkReviewedRefreshesTheWaitingTimeAndWritesAuditEntry()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using var db = await CreateMigratedContextAsync(databasePath);
            var timestamp = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
            var managementService = new WaitingTimeManagementService(
                db,
                NullLogger<WaitingTimeManagementService>.Instance,
                new HttpContextAccessor(),
                new FixedTimeProvider(timestamp));
            var waitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                "Testviðgerð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));

            await managementService.MarkReviewedAsync(waitingTime.Id, waitingTime.Version, "heilsunet\\reviewer");

            var entry = Assert.Single(db.WaitingTimeHistoryEntries.Where(item => item.Action == HistoryAction.Reviewed));
            Assert.Equal("heilsunet\\reviewer", entry.Username);
            Assert.Equal(timestamp.UtcDateTime, entry.Timestamp);
            Assert.Equal(timestamp.UtcDateTime, waitingTime.UpdatedAt);
            Assert.Equal(2, waitingTime.Version);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task CreateWritesFilterableAuditEntryWithRequestId()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var httpContext = new DefaultHttpContext { TraceIdentifier = "test-request-42" };
                var timestamp = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor { HttpContext = httpContext },
                    new FixedTimeProvider(timestamp));
                var readService = new WaitingTimeReadService(db, new FixedTimeProvider(timestamp));

                var waitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                    "Testviðgerð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));

                var query = new HistoryQuery(
                    Hospital: Hospital.Klaksvikar,
                    Name: waitingTime.Name,
                    Action: HistoryAction.Created,
                    Username: "heilsunet\\test-user");
                var entries = await readService.GetHistoryAsync(query);
                var page = await readService.GetHistoryPageAsync(query);

                var entry = Assert.Single(entries);
                Assert.Single(page.Entries);
                Assert.Equal(1, page.TotalCount);
                Assert.Equal("test-request-42", entry.RequestId);
                Assert.Equal(HistoryAction.Created, entry.Action);
                Assert.Equal(timestamp.UtcDateTime, waitingTime.UpdatedAt);
                Assert.Equal(timestamp.UtcDateTime, entry.Timestamp);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task CreateRollsBackWhenItsAuditEntryCannotBeWritten()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TRIGGER FailHistoryInsert
                    BEFORE INSERT ON WaitingTimeHistoryEntries
                    BEGIN
                        SELECT RAISE(ABORT, 'Audit insert failed');
                    END;
                    """);
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);

                await Assert.ThrowsAsync<DbUpdateException>(() => managementService.CreateAsync(new WaitingTimeCreate(
                    "Óskrásett viðgerð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user")));

                db.ChangeTracker.Clear();
                Assert.Empty(await db.WaitingTimes.ToListAsync());
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task MigrationPreventsAuditEntryChanges()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var waitingTime = new WaitingTime
                {
                    Name = "Testviðgerð",
                    Hospital = Hospital.Klaksvikar,
                    WaitValue = 3,
                    Unit = WaitUnit.Vikur
                };
                db.WaitingTimes.Add(waitingTime);
                await db.SaveChangesAsync();

                db.WaitingTimeHistoryEntries.Add(new WaitingTimeHistoryEntry
                {
                    WaitingTimeId = waitingTime.Id,
                    RequestId = "test-request-43",
                    Hospital = waitingTime.Hospital,
                    Name = waitingTime.Name,
                    Action = HistoryAction.Created
                });
                await db.SaveChangesAsync();

                await Assert.ThrowsAsync<SqliteException>(() =>
                    db.Database.ExecuteSqlRawAsync("UPDATE WaitingTimeHistoryEntries SET Name = 'Changed' WHERE Id = 1"));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task AllMigrationsApplyToANewDatabase()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using var db = await CreateMigratedContextAsync(databasePath);

            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ActiveQueryExcludesDeactivatedWaitingTimes()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);
                var readService = new WaitingTimeReadService(db, TimeProvider.System);
                var active = await managementService.CreateAsync(new WaitingTimeCreate(
                    "Virkin viðgerð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));
                var inactive = await managementService.CreateAsync(new WaitingTimeCreate(
                    "Óvirkin viðgerð", Hospital.Klaksvikar, null, 2, WaitUnit.Vikur, null, "heilsunet\\test-user"));
                await managementService.DeactivateAsync(inactive.Id, inactive.Version, "heilsunet\\test-user");

                var results = await readService.GetActiveAsync(Hospital.Klaksvikar, null);
                var history = await readService.GetHistoryAsync(new HistoryQuery(
                    Name: inactive.Name,
                    Action: HistoryAction.Deactivated));

                var result = Assert.Single(results);
                Assert.Equal(active.Id, result.Id);
                var entry = Assert.Single(history);
                Assert.Equal(inactive.Id, entry.WaitingTimeId);
                Assert.Equal(inactive.Hospital, entry.Hospital);
                Assert.Equal(HistoryAction.Deactivated, entry.Action);
                Assert.Equal(inactive.WaitValue, entry.OldWaitValue);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReactivateRestoresTheWaitingTimeAndWritesANewAuditEntry()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using var db = await CreateMigratedContextAsync(databasePath);
            var managementService = new WaitingTimeManagementService(
                db,
                NullLogger<WaitingTimeManagementService>.Instance,
                new HttpContextAccessor(),
                TimeProvider.System);
            var waitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                "Endurstovna bíðitíð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));

            await managementService.DeactivateAsync(waitingTime.Id, waitingTime.Version, "heilsunet\\test-user");
            var restored = await managementService.ReactivateAsync(waitingTime.Id, waitingTime.Version, "heilsunet\\restorer");

            Assert.True(restored.IsActive);
            Assert.Equal(3, restored.Version);
            var entries = await db.WaitingTimeHistoryEntries
                .Where(entry => entry.WaitingTimeId == waitingTime.Id)
                .OrderBy(entry => entry.Id)
                .ToListAsync();
            Assert.Equal(3, entries.Count);
            var reactivated = Assert.Single(entries, entry => entry.Action == HistoryAction.Reactivated);
            Assert.Equal("heilsunet\\restorer", reactivated.Username);
            Assert.Equal(waitingTime.WaitValue, reactivated.NewWaitValue);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReadServiceReturnsDetachedEntities()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);
                var readService = new WaitingTimeReadService(db, TimeProvider.System);
                await managementService.CreateAsync(new WaitingTimeCreate(
                    "Frákoplað viðgerð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));
                db.ChangeTracker.Clear();

                var waitingTime = Assert.Single(await readService.GetActiveAsync(Hospital.Klaksvikar, null));

                Assert.Equal(EntityState.Detached, db.Entry(waitingTime).State);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ManagementServiceRejectsInvalidWaitingTimeInput()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);

                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => managementService.CreateAsync(new WaitingTimeCreate(
                    "Ógild viðgerð", Hospital.Klaksvikar, null, -1, WaitUnit.Vikur, null, "heilsunet\\test-user")));
                await Assert.ThrowsAsync<ArgumentException>(() => managementService.CreateAsync(new WaitingTimeCreate(
                    "Viðgerð uttan depil", Hospital.Landssjukrahusid, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user")));
                Assert.Empty(await db.WaitingTimes.ToListAsync());
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ManagementServiceNormalizesWaitingTimeInput()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);

                var waitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                    "  Viðgerð  ", Hospital.Klaksvikar, Department.Medisinskidepilin, 3, WaitUnit.Vikur, "  Viðmerking  ", "heilsunet\\test-user"));

                Assert.Equal("Viðgerð", waitingTime.Name);
                Assert.Equal("Viðmerking", waitingTime.Note);
                Assert.Null(waitingTime.Department);

                var sharedWaitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                    "Felags viðgerð", Hospital.Sjukrahusverkid, Department.Medisinskidepilin, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));

                Assert.Equal(Hospital.Sjukrahusverkid, sharedWaitingTime.Hospital);
                Assert.Null(sharedWaitingTime.Department);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task OperationsSummaryCountsActiveWaitingTimesAndHistory()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
                var staleLongWait = new WaitingTime
                {
                    Name = "Gomul long bíðitíð",
                    Hospital = Hospital.Landssjukrahusid,
                    WaitValue = WaitDuration.LongWaitDays + 1,
                    Unit = WaitUnit.Dagar,
                    UpdatedAt = now.AddMonths(-4).UtcDateTime
                };
                var currentWait = new WaitingTime
                {
                    Name = "Nýggj bíðitíð",
                    Hospital = Hospital.Klaksvikar,
                    WaitValue = 2,
                    Unit = WaitUnit.Vikur,
                    UpdatedAt = now.UtcDateTime
                };
                var inactiveLongWait = new WaitingTime
                {
                    Name = "Óvirkin bíðitíð",
                    Hospital = Hospital.Suduroyar,
                    WaitValue = WaitDuration.LongWaitDays + 1,
                    Unit = WaitUnit.Dagar,
                    UpdatedAt = now.AddMonths(-4).UtcDateTime,
                    IsActive = false
                };
                db.WaitingTimes.AddRange(staleLongWait, currentWait, inactiveLongWait);
                await db.SaveChangesAsync();
                db.WaitingTimeHistoryEntries.AddRange(
                    new WaitingTimeHistoryEntry { WaitingTimeId = staleLongWait.Id, Timestamp = now.AddDays(-31).UtcDateTime, Hospital = staleLongWait.Hospital, Name = staleLongWait.Name },
                    new WaitingTimeHistoryEntry { WaitingTimeId = currentWait.Id, Timestamp = now.AddDays(-3).UtcDateTime, Hospital = currentWait.Hospital, Name = currentWait.Name });
                await db.SaveChangesAsync();
                var readService = new WaitingTimeReadService(db, new FixedTimeProvider(now));

                var summary = await readService.GetOperationsSummaryAsync(30, 3, 12);

                Assert.Equal(new OperationsSummary(1, 1, 1, 1, 1), summary);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task CombinedUpdateWritesOneAuditEntry()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);
                var waitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                    "Upprunaligt navn", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));

                await managementService.UpdateAsync(new WaitingTimeUpdate(
                    waitingTime.Id,
                    waitingTime.Version,
                    "Nýtt navn",
                    Hospital.Landssjukrahusid,
                    Department.Medisinskidepilin,
                    2,
                    WaitUnit.Manadir,
                    "Nýggj viðmerking",
                    "heilsunet\\test-user"));

                var entries = await db.WaitingTimeHistoryEntries
                    .Where(entry => entry.WaitingTimeId == waitingTime.Id)
                    .OrderBy(entry => entry.Id)
                    .ToListAsync();

                var update = Assert.Single(entries, entry => entry.Action == HistoryAction.Updated);
                Assert.Equal(2, entries.Count);
                Assert.Equal("Nýtt navn", update.Name);
                Assert.Equal(Hospital.Landssjukrahusid, update.Hospital);
                Assert.Equal(Department.Medisinskidepilin, update.Department);
                Assert.Equal(3, update.OldWaitValue);
                Assert.Equal(2, update.NewWaitValue);
                Assert.Contains("Bíðitíð:", update.Detail);
                Assert.Contains("Navn:", update.Detail);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task UpdateRejectsAStaleVersion()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using (var db = await CreateMigratedContextAsync(databasePath))
            {
                var managementService = new WaitingTimeManagementService(
                    db,
                    NullLogger<WaitingTimeManagementService>.Instance,
                    new HttpContextAccessor(),
                    TimeProvider.System);
                var waitingTime = await managementService.CreateAsync(new WaitingTimeCreate(
                    "Bíðitíð", Hospital.Klaksvikar, null, 3, WaitUnit.Vikur, null, "heilsunet\\test-user"));
                var staleVersion = waitingTime.Version;

                await managementService.UpdateAsync(new WaitingTimeUpdate(
                    waitingTime.Id, staleVersion, waitingTime.Name, waitingTime.Hospital, null, 4, WaitUnit.Vikur, null, "heilsunet\\test-user"));

                await Assert.ThrowsAsync<WaitingTimeConcurrencyException>(() => managementService.UpdateAsync(new WaitingTimeUpdate(
                    waitingTime.Id, staleVersion, waitingTime.Name, waitingTime.Hospital, null, 5, WaitUnit.Vikur, null, "heilsunet\\test-user")));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task LifecycleOperationsRejectMissingWaitingTimes()
    {
        var databasePath = NewDatabasePath();
        try
        {
            await using var db = await CreateMigratedContextAsync(databasePath);
            var managementService = new WaitingTimeManagementService(
                db,
                NullLogger<WaitingTimeManagementService>.Instance,
                new HttpContextAccessor(),
                TimeProvider.System);

            await Assert.ThrowsAsync<WaitingTimeNotFoundException>(() => managementService.UpdateAsync(new WaitingTimeUpdate(
                999, 1, "Missing", Hospital.Klaksvikar, null, 1, WaitUnit.Dagar, null, "test-user")));
            await Assert.ThrowsAsync<WaitingTimeNotFoundException>(() => managementService.MarkReviewedAsync(999, 1, "test-user"));
            await Assert.ThrowsAsync<WaitingTimeNotFoundException>(() => managementService.DeactivateAsync(999, 1, "test-user"));
            await Assert.ThrowsAsync<WaitingTimeNotFoundException>(() => managementService.ReactivateAsync(999, 1, "test-user"));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static async Task<ApplicationDbContext> CreateMigratedContextAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    private static string NewDatabasePath() => Path.Combine(Path.GetTempPath(), $"wt-blazor-tests-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}