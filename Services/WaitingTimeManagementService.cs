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
        var input = NormalizeInput(create.Name, create.Hospital, create.Department, create.WaitValue, create.Unit, create.Note, create.EstimateStatus, create.ReferralEndpoint, create.ResponsibleDepartment, create.ComparisonCode, create.WaitUpperValue);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var entity = new WaitingTime
        {
            Name = input.Name,
            Hospital = input.Hospital,
            Department = input.Department,
            WaitValue = input.WaitValue,
            WaitUpperValue = input.UpperValue,
            Unit = input.Unit,
            Note = input.Note,
            EstimateStatus = input.Status,
            ReferralEndpoint = input.Endpoint,
            ResponsibleDepartment = input.Owner,
            ComparisonCode = input.ComparisonCode,
            UpdatedAt = now,
            VerifiedAt = input.Status == EstimateStatus.Available ? now : null,
            IsActive = true,
            Version = 1
        };
        db.WaitingTimes.Add(entity);
        await SaveChangesAsync();

        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Created, "Nýggj bíðitíð stovnað", create.Username);
        historyEntry.NewWaitValue = input.Status == EstimateStatus.Available ? input.WaitValue : null;
        historyEntry.NewUnit = input.Unit;
        historyEntry.NewWaitDays = input.Status == EstimateStatus.Available ? WaitDuration.ToDays(input.WaitValue, input.Unit) : null;
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Waiting time created. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, create.Username, requestId);
        return entity;
    }

    public async Task UpdateAsync(WaitingTimeUpdate update)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var input = NormalizeInput(update.Name, update.Hospital, update.Department, update.WaitValue, update.Unit, update.Note, update.EstimateStatus, update.ReferralEndpoint, update.ResponsibleDepartment, update.ComparisonCode, update.WaitUpperValue);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await db.WaitingTimes.FindAsync(update.Id) ?? throw new WaitingTimeNotFoundException(update.Id);
        if (entity.Version != update.ExpectedVersion)
        {
            db.ChangeTracker.Clear();
            throw new WaitingTimeConcurrencyException();
        }
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = update.ExpectedVersion;

        var largeChange = EstimateChange.RequiresConfirmation(entity, input.Status, input.Endpoint, input.WaitValue, input.Unit, input.UpperValue);
        if (largeChange && !update.ConfirmLargeChange)
            throw new LargeEstimateChangeException();

        var oldStatus = entity.EstimateStatus;
        var oldWait = entity.WaitValue;
        var oldUpper = entity.WaitUpperValue;
        var oldUnit = entity.Unit;
        var oldNote = entity.Note ?? string.Empty;
        var newNote = input.Note ?? string.Empty;
        var definitionChanged = entity.ReferralEndpoint != input.Endpoint;
        var statusChanged = oldStatus != input.Status;
        var waitChanged = statusChanged || definitionChanged || input.Status == EstimateStatus.Available && (oldWait != input.WaitValue || oldUnit != input.Unit || oldUpper != input.UpperValue);
        var noteChanged = oldNote != newNote;
        var changes = new List<string>();
        if (largeChange) changes.Add("Large estimate change confirmed");
        if (statusChanged) changes.Add($"Estimate status: {oldStatus} → {input.Status}");
        if (definitionChanged) changes.Add($"Referral endpoint: {entity.ReferralEndpoint} → {input.Endpoint}");
        if (entity.ComparisonCode != input.ComparisonCode) changes.Add($"Comparison procedure code: {entity.ComparisonCode} → {input.ComparisonCode}");
        if (entity.ResponsibleDepartment != input.Owner) changes.Add($"Responsible department: {entity.ResponsibleDepartment} → {input.Owner}");
        if (waitChanged) changes.Add($"Bíðitíð: {(oldStatus == EstimateStatus.Available ? WaitText(oldWait, oldUnit) : ReferralEstimate.StatusLabel(oldStatus))} → {(input.Status == EstimateStatus.Available ? WaitText(input.WaitValue, input.Unit) : ReferralEstimate.StatusLabel(input.Status))}");
        if (entity.Name != input.Name) changes.Add($"Navn: {entity.Name} → {input.Name}");
        if (entity.Hospital != input.Hospital) changes.Add($"Sjúkrahús: {DisplayNames.For(entity.Hospital)} → {DisplayNames.For(input.Hospital)}");
        if (entity.Department != input.Department) changes.Add("Depil broyttur");
        if (noteChanged) changes.Add("Viðmerking broytt");

        if (changes.Count == 0) return;

        if (definitionChanged || statusChanged || (oldUpper is not null) != (input.UpperValue is not null)) entity.EstimateSeries++;
        if (oldUpper != input.UpperValue) changes.Add($"Estimate upper bound: {oldUpper} → {input.UpperValue}");
        entity.EstimateStatus = input.Status;
        entity.ReferralEndpoint = input.Endpoint;
        entity.ResponsibleDepartment = input.Owner;
        entity.ComparisonCode = input.ComparisonCode;
        entity.Name = input.Name;
        entity.Hospital = input.Hospital;
        entity.Department = input.Department;
        entity.WaitValue = input.WaitValue;
        entity.WaitUpperValue = input.UpperValue;
        entity.Unit = input.Unit;
        entity.Note = newNote;
        // Metadata edits do not verify the waiting-time estimate.
        if (waitChanged) entity.UpdatedAt = now;
        if (input.Status != EstimateStatus.Available) entity.VerifiedAt = null;
        else if (statusChanged || oldWait != input.WaitValue || oldUnit != input.Unit || oldUpper != input.UpperValue) entity.VerifiedAt = now;
        entity.Version++;

        var action = waitChanged ? HistoryAction.Updated : HistoryAction.Edited;
        var historyEntry = CreateHistoryEntry(entity, requestId, now, action, string.Join(" | ", changes), update.Username);
        historyEntry.OldWaitUpperValue = oldUpper;
        historyEntry.OldWaitValue = oldStatus == EstimateStatus.Available ? oldWait : null;
        historyEntry.OldUnit = oldUnit;
        historyEntry.OldWaitDays = oldStatus == EstimateStatus.Available ? WaitDuration.ToDays(oldWait, oldUnit) : null;
        historyEntry.NewWaitValue = input.Status == EstimateStatus.Available ? input.WaitValue : null;
        historyEntry.NewUnit = input.Unit;
        historyEntry.NewWaitDays = input.Status == EstimateStatus.Available ? WaitDuration.ToDays(input.WaitValue, input.Unit) : null;
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time updated. WaitingTimeId: {WaitingTimeId}, Name: {Name}, WaitChanged: {WaitChanged}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, waitChanged, update.Username, requestId);
    }

    public async Task DeactivateAsync(int id, int expectedVersion, string username, string? reason = null)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await db.WaitingTimes.FindAsync(id) ?? throw new WaitingTimeNotFoundException(id);
        if (entity.Version != expectedVersion)
        {
            db.ChangeTracker.Clear();
            throw new WaitingTimeConcurrencyException();
        }
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = expectedVersion;
        entity.IsActive = false;
        entity.Version++;

        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Deactivated,
            string.IsNullOrWhiteSpace(reason) ? "Bíðitíð tikin av almennu síðuni" : $"Bíðitíð tikin av almennu síðuni. Orsøk: {reason.Trim()}", username);
        historyEntry.OldWaitUpperValue = entity.WaitUpperValue;
        historyEntry.OldWaitValue = entity.EstimateStatus == EstimateStatus.Available ? entity.WaitValue : null;
        historyEntry.OldUnit = entity.Unit;
        historyEntry.OldWaitDays = entity.EstimateStatus == EstimateStatus.Available ? WaitDuration.ToDays(entity.WaitValue, entity.Unit) : null;
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time deactivated. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, username, requestId);
    }

    public async Task<WaitingTime> ReactivateAsync(int id, int expectedVersion, string username)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await db.WaitingTimes.FindAsync(id) ?? throw new WaitingTimeNotFoundException(id);
        if (entity.Version != expectedVersion)
        {
            db.ChangeTracker.Clear();
            throw new WaitingTimeConcurrencyException();
        }
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = expectedVersion;

        entity.IsActive = true;
        entity.Version++;

        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Reactivated, "Bíðitíð løgd aftur á almennu síðuna", username);
        historyEntry.OldWaitUpperValue = entity.WaitUpperValue;
        historyEntry.OldWaitValue = entity.EstimateStatus == EstimateStatus.Available ? entity.WaitValue : null;
        historyEntry.OldUnit = entity.Unit;
        historyEntry.OldWaitDays = entity.EstimateStatus == EstimateStatus.Available ? WaitDuration.ToDays(entity.WaitValue, entity.Unit) : null;
        historyEntry.NewWaitValue = entity.EstimateStatus == EstimateStatus.Available ? entity.WaitValue : null;
        historyEntry.NewUnit = entity.Unit;
        historyEntry.NewWaitDays = entity.EstimateStatus == EstimateStatus.Available ? WaitDuration.ToDays(entity.WaitValue, entity.Unit) : null;
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time reactivated. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, username, requestId);
        return entity;
    }

    public async Task MarkReviewedAsync(int id, int expectedVersion, string username)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await db.WaitingTimes.FindAsync(id) ?? throw new WaitingTimeNotFoundException(id);
        if (entity.Version != expectedVersion)
        {
            db.ChangeTracker.Clear();
            throw new WaitingTimeConcurrencyException();
        }
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = expectedVersion;

        entity.UpdatedAt = now;
        entity.VerifiedAt = entity.EstimateStatus == EstimateStatus.Available ? now : null;
        entity.Version++;
        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Reviewed, "Bíðitíð eftirkannað uttan broyting", username);
        historyEntry.OldWaitUpperValue = entity.WaitUpperValue;
        historyEntry.OldWaitValue = entity.EstimateStatus == EstimateStatus.Available ? entity.WaitValue : null;
        historyEntry.OldUnit = entity.Unit;
        historyEntry.OldWaitDays = entity.EstimateStatus == EstimateStatus.Available ? WaitDuration.ToDays(entity.WaitValue, entity.Unit) : null;
        historyEntry.NewWaitValue = entity.EstimateStatus == EstimateStatus.Available ? entity.WaitValue : null;
        historyEntry.NewUnit = entity.Unit;
        historyEntry.NewWaitDays = entity.EstimateStatus == EstimateStatus.Available ? WaitDuration.ToDays(entity.WaitValue, entity.Unit) : null;
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        logger.LogInformation("Waiting time reviewed. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, username, requestId);
    }

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

    private static WaitingTimeInput NormalizeInput(string name, Hospital hospital, Department? department, int wait, WaitUnit unit, string? note, EstimateStatus status, ReferralEndpoint endpoint, string? owner, string? comparisonCode, int? upperValue)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (!Enum.IsDefined(endpoint)) throw new ArgumentOutOfRangeException(nameof(endpoint));
        comparisonCode = string.IsNullOrWhiteSpace(comparisonCode) ? null : comparisonCode.Trim().ToUpperInvariant();
        if (comparisonCode?.Length > 100) throw new ArgumentException("Comparison code cannot exceed 100 characters.", nameof(comparisonCode));
        owner = string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
        if (owner?.Length > 200) throw new ArgumentException("Responsible department cannot exceed 200 characters.", nameof(owner));
        // Unavailable values are stored as a placeholder but never represented as zero.
        if (status != EstimateStatus.Available) { wait = 0; upperValue = null; }
        if (upperValue is { } upper && (upper < wait || upper > WaitDuration.MaximumValue(unit)))
            throw new ArgumentOutOfRangeException(nameof(upperValue), "The upper bound must be at least the lower bound and within the supported duration.");
        name = name?.Trim() ?? string.Empty;
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A waiting time must have a name.", nameof(name));
        if (name.Length > 200) throw new ArgumentException("A waiting time name cannot exceed 200 characters.", nameof(name));
        if (wait < 0) throw new ArgumentOutOfRangeException(nameof(wait), "A waiting time cannot be negative.");
        if (!Enum.IsDefined(hospital)) throw new ArgumentOutOfRangeException(nameof(hospital));
        if (hospital == Hospital.Landssjukrahusid && department is null) throw new ArgumentException("Landssjúkrahúsið requires a department.", nameof(department));
        if (department is not null && !Enum.IsDefined(department.Value)) throw new ArgumentOutOfRangeException(nameof(department));
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));
        _ = WaitDuration.ToDays(wait, unit);
        if (note?.Length > 500) throw new ArgumentException("A note cannot exceed 500 characters.", nameof(note));
        return new WaitingTimeInput(name, hospital, hospital == Hospital.Landssjukrahusid ? department : null, wait, unit, note, status, endpoint, owner, comparisonCode, upperValue);
    }

    private sealed record WaitingTimeInput(string Name, Hospital Hospital, Department? Department, int WaitValue, WaitUnit Unit, string? Note, EstimateStatus Status, ReferralEndpoint Endpoint, string? Owner, string? ComparisonCode, int? UpperValue);

    private static WaitingTimeHistoryEntry CreateHistoryEntry(WaitingTime entity, string requestId, DateTime timestamp, HistoryAction action, string detail, string username) => new()
    {
        WaitingTimeId = entity.Id,
        RequestId = requestId,
        Timestamp = timestamp,
        Hospital = entity.Hospital,
        Department = entity.Department,
        Name = entity.Name,
        EstimateStatus = entity.EstimateStatus,
        ReferralEndpoint = entity.ReferralEndpoint,
        ResponsibleDepartment = entity.ResponsibleDepartment,
        ComparisonCode = entity.ComparisonCode,
        EstimateSeries = entity.EstimateSeries,
        NewWaitUpperValue = entity.WaitUpperValue,
        Action = action,
        Detail = detail,
        Username = username
    };

    private string RequestId() => httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}