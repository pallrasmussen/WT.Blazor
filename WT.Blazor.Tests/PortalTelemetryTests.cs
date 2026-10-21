using System.Diagnostics.Metrics;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class PortalTelemetryTests
{
    [Fact]
    public void RecordsPublicRefreshLifecycleMetrics()
    {
        var measurements = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "WT.Blazor")
                meterListener.EnableMeasurementEvents(instrument, measurements);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, state) =>
            ((List<string>)state!).Add(instrument.Name));
        listener.Start();

        var telemetry = new PortalTelemetry();
        telemetry.RecordPublicRefreshStarted("manual");
        telemetry.RecordPublicRefreshSucceeded("manual");
        telemetry.RecordPublicRefreshFailed("manual");
        telemetry.RecordPublicRefreshDiscarded("manual");

        Assert.Contains("wt_blazor.public_refresh.requests", measurements);
        Assert.Contains("wt_blazor.public_refresh.successes", measurements);
        Assert.Contains("wt_blazor.public_refresh.failures", measurements);
        Assert.Contains("wt_blazor.public_refresh.discarded", measurements);
    }
}
