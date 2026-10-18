namespace WT.Blazor.Services;

public sealed class WaitingTimeNotFoundException(int waitingTimeId)
    : InvalidOperationException($"Waiting time {waitingTimeId} was not found.");