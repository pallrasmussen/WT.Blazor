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
        PortalSettingsValidation.Normalize(settings);
        PortalSettingsValidation.ThrowIfInvalid(settings);

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
            || current.EstimateExpiryMonths != settings.EstimateExpiryMonths
            || current.YellowMaximumAgeMonths != settings.YellowMaximumAgeMonths
            || current.LongWaitMonths != settings.LongWaitMonths
            || current.ReferralInstructionsFo != settings.ReferralInstructionsFo
            || current.ReferralInstructionsEn != settings.ReferralInstructionsEn
            || current.ContactGuidanceFo != settings.ContactGuidanceFo
            || current.ContactGuidanceEn != settings.ContactGuidanceEn
            || current.PublishPatientGuidance != settings.PublishPatientGuidance;
        if (!changed) return;

        db.PortalSettingsAuditEntries.Add(new PortalSettingsAuditEntry
        {
            OldReferralInstructionsFo = current.ReferralInstructionsFo,
            OldReferralInstructionsEn = current.ReferralInstructionsEn,
            OldContactGuidanceFo = current.ContactGuidanceFo,
            OldContactGuidanceEn = current.ContactGuidanceEn,
            NewReferralInstructionsFo = settings.ReferralInstructionsFo,
            NewReferralInstructionsEn = settings.ReferralInstructionsEn,
            NewContactGuidanceFo = settings.ContactGuidanceFo,
            NewContactGuidanceEn = settings.ContactGuidanceEn,
            OldPublishPatientGuidance = current.PublishPatientGuidance,
            NewPublishPatientGuidance = settings.PublishPatientGuidance,
            Timestamp = timeProvider.GetUtcNow().UtcDateTime,
            Username = username,
            OldGreenMaximumAgeMonths = current.GreenMaximumAgeMonths,
            OldEstimateExpiryMonths = current.EstimateExpiryMonths,
            NewEstimateExpiryMonths = settings.EstimateExpiryMonths,
            OldYellowMaximumAgeMonths = current.YellowMaximumAgeMonths,
            OldLongWaitMonths = current.LongWaitMonths,
            NewGreenMaximumAgeMonths = settings.GreenMaximumAgeMonths,
            NewYellowMaximumAgeMonths = settings.YellowMaximumAgeMonths,
            NewLongWaitMonths = settings.LongWaitMonths
        });
        current.EstimateExpiryMonths = settings.EstimateExpiryMonths;
        current.GreenMaximumAgeMonths = settings.GreenMaximumAgeMonths;
        current.YellowMaximumAgeMonths = settings.YellowMaximumAgeMonths;
        current.LongWaitMonths = settings.LongWaitMonths;
        current.ReferralInstructionsFo = settings.ReferralInstructionsFo;
        current.ReferralInstructionsEn = settings.ReferralInstructionsEn;
        current.ContactGuidanceFo = settings.ContactGuidanceFo;
        current.ContactGuidanceEn = settings.ContactGuidanceEn;
        current.PublishPatientGuidance = settings.PublishPatientGuidance;
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

}
