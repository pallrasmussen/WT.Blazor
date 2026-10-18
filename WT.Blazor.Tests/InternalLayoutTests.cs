using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Globalization;
using WT.Blazor.Components.Layout;
using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class InternalLayoutTests : BunitContext
{
    [Fact]
    public void IncludesAnArrowLedLinkBackToThePublicWaitingTimesPage()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fo");
        try
        {
        Services.Configure<AdGroupsOptions>(_ => { });
        Services.AddSingleton<HealthCheckService>(new HealthyHealthChecks());
        Services.AddSingleton(DeploymentManifest.Unknown);
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<InternalLayout>();
        var backLink = cut.Find("a.internal-nav-back");

        Assert.Equal("/", backLink.GetAttribute("href"));
        Assert.Contains("Aftur til bíðitíðir", backLink.TextContent);
        Assert.Contains("←", backLink.TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private sealed class HealthyHealthChecks : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool>? predicate = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero));
    }
}