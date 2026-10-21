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
        var input = NormalizeInput(create.Name, create.Hospital, create.Department, create.WaitValue, create.Unit, create.Note);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var entity = new WaitingTime
        {
            Name = input.Name,
            Hospital = input.Hospital,
            Department = input.Department,
            WaitValue = input.WaitValue,
            Unit = input.Unit,
            Note = input.Note,
            UpdatedAt = now,
            IsActive = true,
            Version = 1
        };
        db.WaitingTimes.Add(entity);
        await SaveChangesAsync();

        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Created, "Nýggj bíðitíð stovnað", create.Username);
        historyEntry.NewWaitValue = input.WaitValue;
        historyEntry.NewUnit = input.Unit;
        historyEntry.NewWaitDays = WaitDuration.ToDays(input.WaitValue, input.Unit);
        db.WaitingTimeHistoryEntries.Add(historyEntry);
        await SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Waiting time created. WaitingTimeId: {WaitingTimeId}, Name: {Name}, User: {Username}, RequestId: {RequestId}", entity.Id, entity.Name, create.Username, requestId);
        return entity;
    }

    public async Task UpdateAsync(WaitingTimeUpdate update)
    {
        using var operation = await DatabaseOperation.EnterAsync(db);
        var input = NormalizeInput(update.Name, update.Hospital, update.Department, update.WaitValue, update.Unit, update.Note);
        var requestId = RequestId();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await db.WaitingTimes.FindAsync(update.Id) ?? throw new WaitingTimeNotFoundException(update.Id);
        if (entity.Version != update.ExpectedVersion) throw new WaitingTimeConcurrencyException();
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = update.ExpectedVersion;

        var oldWait = entity.WaitValue;
        var oldUnit = entity.Unit;
        var oldNote = entity.Note ?? string.Empty;
        var newNote = input.Note ?? string.Empty;
        var waitChanged = oldWait != input.WaitValue || oldUnit != input.Unit;
        var noteChanged = oldNote != newNote;
        var changes = new List<string>();
        if (waitChanged) changes.Add($"Bíðitíð: {WaitText(oldWait, oldUnit)} → {WaitText(input.WaitValue, input.Unit)}");
        if (entity.Name != input.Name) changes.Add($"Navn: {entity.Name} → {input.Name}");
        if (entity.Hospital != input.Hospital) changes.Add($"Sjúkrahús: {DisplayNames.For(entity.Hospital)} → {DisplayNames.For(input.Hospital)}");
        if (entity.Department != input.Department) changes.Add("Depil broyttur");
        if (noteChanged) changes.Add("Viðmerking broytt");

        if (changes.Count == 0) return;

        entity.Name = input.Name;
        entity.Hospital = input.Hospital;
        entity.Department = input.Department;
        entity.WaitValue = input.WaitValue;
        entity.Unit = input.Unit;
        entity.Note = newNote;
        entity.UpdatedAt = now;
        entity.Version++;

        var action = waitChanged ? HistoryAction.Updated : HistoryAction.Edited;
        var historyEntry = CreateHistoryEntry(entity, requestId, now, action, string.Join(" | ", changes), update.Username);
        historyEntry.OldWaitValue = oldWait;
        historyEntry.OldUnit = oldUnit;
        historyEntry.OldWaitDays = WaitDuration.ToDays(oldWait, oldUnit);
        historyEntry.NewWaitValue = input.WaitValue;
        historyEntry.NewUnit = input.Unit;
        historyEntry.NewWaitDays = WaitDuration.ToDays(input.WaitValue, input.Unit);
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
        if (entity.Version != expectedVersion) throw new WaitingTimeConcurrencyException();
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = expectedVersion;
        entity.IsActive = false;
        entity.Version++;

        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Deactivated,
            string.IsNullOrWhiteSpace(reason) ? "Bíðitíð tikin av almennu síðuni" : $"Bíðitíð tikin av almennu síðuni. Orsøk: {reason.Trim()}", username);
        historyEntry.OldWaitValue = entity.WaitValue;
        historyEntry.OldUnit = entity.Unit;
        historyEntry.OldWaitDays = WaitDuration.ToDays(entity.WaitValue, entity.Unit);
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
        if (entity.Version != expectedVersion) throw new WaitingTimeConcurrencyException();
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = expectedVersion;

        entity.IsActive = true;
        entity.UpdatedAt = now;
        entity.Version++;

        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Reactivated, "Bíðitíð løgd aftur á almennu síðuna", username);
        historyEntry.OldWaitValue = entity.WaitValue;
        historyEntry.OldUnit = entity.Unit;
        historyEntry.OldWaitDays = WaitDuration.ToDays(entity.WaitValue, entity.Unit);
        historyEntry.NewWaitValue = entity.WaitValue;
        historyEntry.NewUnit = entity.Unit;
        historyEntry.NewWaitDays = WaitDuration.ToDays(entity.WaitValue, entity.Unit);
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
        if (entity.Version != expectedVersion) throw new WaitingTimeConcurrencyException();
        db.Entry(entity).Property(waitingTime => waitingTime.Version).OriginalValue = expectedVersion;

        entity.UpdatedAt = now;
        entity.Version++;
        var historyEntry = CreateHistoryEntry(entity, requestId, now, HistoryAction.Reviewed, "Bíðitíð eftirkannað uttan broyting", username);
        historyEntry.OldWaitValue = entity.WaitValue;
        historyEntry.OldUnit = entity.Unit;
        historyEntry.OldWaitDays = WaitDuration.ToDays(entity.WaitValue, entity.Unit);
        historyEntry.NewWaitValue = entity.WaitValue;
        historyEntry.NewUnit = entity.Unit;
        historyEntry.NewWaitDays = WaitDuration.ToDays(entity.WaitValue, entity.Unit);
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

    private static WaitingTimeInput NormalizeInput(string name, Hospital hospital, Department? department, int wait, WaitUnit unit, string? note)
    {
        name = name?.Trim() ?? string.Empty;
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A waiting time must have a name.", nameof(name));
        if (name.Length > 200) throw new ArgumentException("A waiting time name cannot exceed 200 characters.", nameof(name));
        if (wait < 0) throw new ArgumentOutOfRangeException(nameof(wait), "A waiting time cannot be negative.");
        if (!Enum.IsDefined(hospital) || hospital == Hospital.Sjukrahusverkid) throw new ArgumentOutOfRangeException(nameof(hospital));
        if (hospital == Hospital.Landssjukrahusid && department is null) throw new ArgumentException("Landssjúkrahúsið requires a department.", nameof(department));
        if (department is not null && !Enum.IsDefined(department.Value)) throw new ArgumentOutOfRangeException(nameof(department));
        if (!Enum.IsDefined(unit)) throw new ArgumentOutOfRangeException(nameof(unit));
        if (note?.Length > 500) throw new ArgumentException("A note cannot exceed 500 characters.", nameof(note));
        return new WaitingTimeInput(name, hospital, hospital == Hospital.Landssjukrahusid ? department : null, wait, unit, note);
    }

    private sealed record WaitingTimeInput(string Name, Hospital Hospital, Department? Department, int WaitValue, WaitUnit Unit, string? Note);

    private static WaitingTimeHistoryEntry CreateHistoryEntry(WaitingTime entity, string requestId, DateTime timestamp, HistoryAction action, string detail, string username) => new()
    {
        WaitingTimeId = entity.Id,
        RequestId = requestId,
        Timestamp = timestamp,
        Hospital = entity.Hospital,
        Department = entity.Department,
        Name = entity.Name,
        Action = action,
        Detail = detail,
        Username = username
    };

    private string RequestId() => httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}