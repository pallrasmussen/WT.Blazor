using Microsoft.AspNetCore.Components.Server.Circuits;

namespace WT.Blazor.Services;

public sealed class CircuitTelemetryHandler(
    PortalTelemetry telemetry,
    ILogger<CircuitTelemetryHandler> logger) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        telemetry.RecordCircuitOpened();
        logger.LogInformation("Blazor circuit opened: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        telemetry.RecordCircuitConnected();
        logger.LogInformation("Blazor circuit connected: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        telemetry.RecordCircuitDisconnected();
        logger.LogWarning("Blazor circuit disconnected: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        telemetry.RecordCircuitClosed();
        logger.LogInformation("Blazor circuit closed: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }
}
