using WT.Blazor.Localization;
using Microsoft.EntityFrameworkCore;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public class WaitingTimeManagementService(
    ApplicationDbContext db,
    ILogger<WaitingTimeManagementService> logger,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IWaitingTimeManagementService
{
    public async Task<WaitingTime> CreateAsync(WaitingTimeCreate create)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var input = WaitingTimeValidation.NormalizeAndValidate(create);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var entity = new WaitingTime
        {
            Name = input.Name,
            Hospital = input.Hospital!.Value,
            Department = input.Department,
            WaitValue = input.WaitValue,
            WaitUpperValue = input.WaitUpperValue,
            Unit = input.Unit,
            Note = input.Note,
            EstimateStatus = input.EstimateStatus,
            ReferralEndpoint = input.ReferralEndpoint,
            ResponsibleDepartment = input.ResponsibleDepartment,
            ComparisonCode = input.ComparisonCode,
            UpdatedAt = now,
            VerifiedAt = input.EstimateStatus == EstimateStatus.Available ? now : null,
            IsActive = true,
            Version = 1
        };
        db.WaitingTimes.Add(entity);
        await SaveChangesAsync();

        var historyEntry = WaitingTimeHistorySnapshot.Create(entity, new(requestId, now, create.Username),
            HistoryAction.Created, "Nýggj bíðitíð stovnað");
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Waiting time created. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, create.Username, requestId);
        return Snapshot(entity);
    }

    public async Task<WaitingTime> UpdateAsync(WaitingTimeUpdate update)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var input = WaitingTimeValidation.NormalizeAndValidate(update);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await FindForUpdateAsync(update.Id, update.ExpectedVersion);

        var largeChange = EstimateChange.RequiresConfirmation(entity, input.EstimateStatus, input.ReferralEndpoint, input.WaitValue, input.Unit, input.WaitUpperValue);
        if (largeChange && !update.ConfirmLargeChange)
            throw new LargeEstimateChangeException();

        var previousDuration = WaitingTimeDurationSnapshot.Capture(entity);
        var oldStatus = entity.EstimateStatus;
        var oldWait = entity.WaitValue;
        var oldUpper = entity.WaitUpperValue;
        var oldUnit = entity.Unit;
        var oldNote = entity.Note ?? string.Empty;
        var newNote = input.Note ?? string.Empty;
        var definitionChanged = entity.ReferralEndpoint != input.ReferralEndpoint;
        var statusChanged = oldStatus != input.EstimateStatus;
        var waitChanged = statusChanged || definitionChanged || input.EstimateStatus == EstimateStatus.Available && (oldWait != input.WaitValue || oldUnit != input.Unit || oldUpper != input.WaitUpperValue);
        var noteChanged = oldNote != newNote;
        var changes = new List<string>();
        if (largeChange) changes.Add("Large estimate change confirmed");
        if (statusChanged) changes.Add($"Estimate status: {oldStatus} → {input.EstimateStatus}");
        if (definitionChanged) changes.Add($"Referral endpoint: {entity.ReferralEndpoint} → {input.ReferralEndpoint}");
        if (entity.ComparisonCode != input.ComparisonCode) changes.Add($"Comparison procedure code: {entity.ComparisonCode} → {input.ComparisonCode}");
        if (entity.ResponsibleDepartment != input.ResponsibleDepartment) changes.Add($"Responsible department: {entity.ResponsibleDepartment} → {input.ResponsibleDepartment}");
        if (waitChanged) changes.Add($"Bíðitíð: {(oldStatus == EstimateStatus.Available ? WaitText(oldWait, oldUnit) : ReferralEstimateText.StatusLabel(oldStatus))} → {(input.EstimateStatus == EstimateStatus.Available ? WaitText(input.WaitValue, input.Unit) : ReferralEstimateText.StatusLabel(input.EstimateStatus))}");
        if (entity.Name != input.Name) changes.Add($"Navn: {entity.Name} → {input.Name}");
        if (entity.Hospital != input.Hospital) changes.Add($"Sjúkrahús: {DisplayNames.For(entity.Hospital)} → {DisplayNames.For(input.Hospital!.Value)}");
        if (entity.Department != input.Department) changes.Add("Depil broyttur");
        if (noteChanged) changes.Add("Viðmerking broytt");

        if (changes.Count == 0) return Snapshot(entity);

        if (definitionChanged || statusChanged || (oldUpper is not null) != (input.WaitUpperValue is not null)) entity.EstimateSeries++;
        if (oldUpper != input.WaitUpperValue) changes.Add($"Estimate upper bound: {oldUpper} → {input.WaitUpperValue}");
        entity.EstimateStatus = input.EstimateStatus;
        entity.ReferralEndpoint = input.ReferralEndpoint;
        entity.ResponsibleDepartment = input.ResponsibleDepartment;
        entity.ComparisonCode = input.ComparisonCode;
        entity.Name = input.Name;
        entity.Hospital = input.Hospital!.Value;
        entity.Department = input.Department;
        entity.WaitValue = input.WaitValue;
        entity.WaitUpperValue = input.WaitUpperValue;
        entity.Unit = input.Unit;
        entity.Note = newNote;
        // Metadata edits do not verify the waiting-time estimate.
        if (waitChanged) entity.UpdatedAt = now;
        if (input.EstimateStatus != EstimateStatus.Available) entity.VerifiedAt = null;
        else if (statusChanged || oldWait != input.WaitValue || oldUnit != input.Unit || oldUpper != input.WaitUpperValue) entity.VerifiedAt = now;
        entity.Version++;

        var action = waitChanged ? HistoryAction.Updated : HistoryAction.Edited;
        var historyEntry = WaitingTimeHistorySnapshot.Create(entity, new(requestId, now, update.Username),
            action, string.Join(" | ", changes), previous: previousDuration);
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time updated. WaitingTimeId: {WaitingTimeId}, Name: {Name}, WaitChanged: {WaitChanged}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, waitChanged, update.Username, requestId);
        return Snapshot(entity);
    }

    public async Task<WaitingTime> DeactivateAsync(int id, int expectedVersion, string username, string? reason = null)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await FindForUpdateAsync(id, expectedVersion);
        entity.IsActive = false;
        entity.Version++;

        var historyEntry = WaitingTimeHistorySnapshot.Create(entity, new(requestId, now, username), HistoryAction.Deactivated,
            string.IsNullOrWhiteSpace(reason) ? "Bíðitíð tikin av almennu síðuni" : $"Bíðitíð tikin av almennu síðuni. Orsøk: {reason.Trim()}",
            previous: WaitingTimeDurationSnapshot.Capture(entity), includeCurrent: false);
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time deactivated. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, username, requestId);
        return Snapshot(entity);
    }

    public async Task<WaitingTime> ReactivateAsync(int id, int expectedVersion, string username)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await FindForUpdateAsync(id, expectedVersion);

        entity.IsActive = true;
        entity.Version++;

        var historyEntry = WaitingTimeHistorySnapshot.Create(entity, new(requestId, now, username),
            HistoryAction.Reactivated, "Bíðitíð løgd aftur á almennu síðuna", previous: WaitingTimeDurationSnapshot.Capture(entity));
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time reactivated. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, username, requestId);
        return Snapshot(entity);
    }

    public async Task<WaitingTime> MarkReviewedAsync(int id, int expectedVersion, string username)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await FindForUpdateAsync(id, expectedVersion);

        entity.UpdatedAt = now;
        entity.VerifiedAt = entity.EstimateStatus == EstimateStatus.Available ? now : null;
        entity.Version++;
        var historyEntry = WaitingTimeHistorySnapshot.Create(entity, new(requestId, now, username),
            HistoryAction.Reviewed, "Bíðitíð eftirkannað uttan broyting", previous: WaitingTimeDurationSnapshot.Capture(entity));
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time reviewed. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, username, requestId);
        return Snapshot(entity);
    }

    private async Task<WaitingTime> FindForUpdateAsync(int id, int expectedVersion)
    {
        var entity = await db.WaitingTimes.FindAsync(id) ?? throw new WaitingTimeNotFoundException(id);
        if (entity.Version != expectedVersion)
        {
            db.ChangeTracker.Clear();
            throw new WaitingTimeConcurrencyException();
        }
        db.Entry(entity).Property(item => item.Version).OriginalValue = expectedVersion;
        return entity;
    }

    // UI callers receive persisted values without sharing EF's tracked entity.
    private WaitingTime Snapshot(WaitingTime entity) => (WaitingTime)db.Entry(entity).CurrentValues.ToObject();

    private async Task SaveChangesAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            db.ChangeTracker.Clear();
            throw new WaitingTimeConcurrencyException(exception);
        }
    }

    private static string WaitText(int value, WaitUnit unit) => $"{value} {DisplayNames.For(unit, value)}";

    private string RequestId() => httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}