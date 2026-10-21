using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public interface IPortalSettingsService
{
    Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default);
}

public sealed class PortalSettingsService(ApplicationDbContext db, PortalSettingsChangeNotifier changeNotifier, TimeProvider timeProvider) : IPortalSettingsService
{
    public async Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        return await db.PortalSettings.AsNoTracking().SingleOrDefaultAsync(settings => settings.Id == 1, cancellationToken)
        ?? new PortalSettings();
    }

    public async Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        Validate(settings);

        // A Blazor circuit can outlive changes saved by other administrators.
        // Load a fresh tracked row for each operation, preserving unrelated entities.
        foreach (var entry in db.ChangeTracker.Entries<PortalSettings>().ToList())
            entry.State = EntityState.Detached;
        var current = await db.PortalSettings.SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (current is null)
        {
            current = new PortalSettings { Id = 1 };
            db.PortalSettings.Add(current);
        }

        if (current.Version != settings.Version)
        {
            db.Entry(current).State = EntityState.Detached;
            throw new PortalSettingsConcurrencyException();
        }

        var changed = current.GreenMaximumAgeMonths != settings.GreenMaximumAgeMonths
            || current.YellowMaximumAgeMonths != settings.YellowMaximumAgeMonths
            || current.LongWaitMonths != settings.LongWaitMonths;
        if (!changed) return;

        db.PortalSettingsAuditEntries.Add(new PortalSettingsAuditEntry
        {
            Timestamp = timeProvider.GetUtcNow().UtcDateTime,
            Username = username,
            OldGreenMaximumAgeMonths = current.GreenMaximumAgeMonths,
            OldYellowMaximumAgeMonths = current.YellowMaximumAgeMonths,
            OldLongWaitMonths = current.LongWaitMonths,
            NewGreenMaximumAgeMonths = settings.GreenMaximumAgeMonths,
            NewYellowMaximumAgeMonths = settings.YellowMaximumAgeMonths,
            NewLongWaitMonths = settings.LongWaitMonths
        });
        current.GreenMaximumAgeMonths = settings.GreenMaximumAgeMonths;
        current.YellowMaximumAgeMonths = settings.YellowMaximumAgeMonths;
        current.LongWaitMonths = settings.LongWaitMonths;
        current.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            db.Entry(current).State = EntityState.Detached;
            foreach (var auditEntry in db.ChangeTracker.Entries<PortalSettingsAuditEntry>()
                         .Where(entry => entry.State == EntityState.Added).ToList())
            {
                auditEntry.State = EntityState.Detached;
            }
            throw new PortalSettingsConcurrencyException(exception);
        }
        changeNotifier.NotifyChanged();
        settings.Version = current.Version;
    }

    public async Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default)
    {
        using var operation = await DatabaseOperation.EnterAsync(db, cancellationToken);
        return await db.PortalSettingsAuditEntries.AsNoTracking()
            .OrderByDescending(entry => entry.Timestamp)
            .Take(maximumEntries)
            .ToListAsync(cancellationToken);
    }

    private static void Validate(PortalSettings settings)
    {
        if (settings.GreenMaximumAgeMonths is < 1 or > 24)
            throw new ArgumentOutOfRangeException(nameof(settings.GreenMaximumAgeMonths));
        if (settings.YellowMaximumAgeMonths is < 2 or > 36 || settings.YellowMaximumAgeMonths <= settings.GreenMaximumAgeMonths)
            throw new ArgumentOutOfRangeException(nameof(settings.YellowMaximumAgeMonths));
        if (settings.LongWaitMonths is < 1 or > 60)
            throw new ArgumentOutOfRangeException(nameof(settings.LongWaitMonths));
    }
}