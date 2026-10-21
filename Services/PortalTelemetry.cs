using System.Diagnostics.Metrics;

namespace WT.Blazor.Services;

public sealed class PortalTelemetry
{
    private static readonly Meter Meter = new("WT.Blazor");
    private readonly Counter<long> publicRefreshes = Meter.CreateCounter<long>("wt_blazor.public_refresh.requests", "{requests}", "Public waiting-time refreshes started.");
    private readonly Counter<long> publicRefreshSuccesses = Meter.CreateCounter<long>("wt_blazor.public_refresh.successes", "{requests}", "Public waiting-time refreshes completed.");
    private readonly Counter<long> publicRefreshFailures = Meter.CreateCounter<long>("wt_blazor.public_refresh.failures", "{requests}", "Public waiting-time refreshes that failed.");
    private readonly Counter<long> publicRefreshDiscards = Meter.CreateCounter<long>("wt_blazor.public_refresh.discarded", "{requests}", "Public waiting-time refreshes discarded because a newer request superseded them.");
    private readonly Counter<long> circuitOpened = Meter.CreateCounter<long>("wt_blazor.circuits.opened", "{circuits}", "Blazor circuits opened.");
    private readonly Counter<long> circuitConnected = Meter.CreateCounter<long>("wt_blazor.circuits.connected", "{connections}", "Blazor circuit connections established.");
    private readonly Counter<long> circuitDisconnected = Meter.CreateCounter<long>("wt_blazor.circuits.disconnected", "{connections}", "Blazor circuit connections lost.");
    private readonly Counter<long> circuitClosed = Meter.CreateCounter<long>("wt_blazor.circuits.closed", "{circuits}", "Blazor circuits closed.");

    public void RecordPublicRefreshStarted(string source) => publicRefreshes.Add(1, new KeyValuePair<string, object?>("source", source));
    public void RecordPublicRefreshSucceeded(string source) => publicRefreshSuccesses.Add(1, new KeyValuePair<string, object?>("source", source));
    public void RecordPublicRefreshFailed(string source) => publicRefreshFailures.Add(1, new KeyValuePair<string, object?>("source", source));
    public void RecordPublicRefreshDiscarded(string source) => publicRefreshDiscards.Add(1, new KeyValuePair<string, object?>("source", source));
    public void RecordCircuitOpened() => circuitOpened.Add(1);
    public void RecordCircuitConnected() => circuitConnected.Add(1);
    public void RecordCircuitDisconnected() => circuitDisconnected.Add(1);
    public void RecordCircuitClosed() => circuitClosed.Add(1);
}
