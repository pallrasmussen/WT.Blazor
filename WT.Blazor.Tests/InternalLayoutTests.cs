using WT.Blazor.Tests.TestInfrastructure;
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

    [Fact]
    public void MarksTheCurrentPageWithinALabelledNavigationLandmark()
    {
        using var culture = new TestCulture("en");
        Services.Configure<AdGroupsOptions>(_ => { });
        Services.AddSingleton<HealthCheckService>(new HealthyHealthChecks());
        Services.AddSingleton(DeploymentManifest.Unknown);
        Services.AddSingleton(TimeProvider.System);
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("/internal");

        var cut = Render<InternalLayout>();

        Assert.Equal("Administration navigation", cut.Find("nav#internal-navigation").GetAttribute("aria-label"));
        Assert.Equal("page", cut.Find("a[href='/internal']").GetAttribute("aria-current"));
        Assert.Empty(cut.FindAll("a[href='/internal?new=true']"));
        Assert.Equal("Administration", cut.Find(".internal-breadcrumb [aria-current='page']").TextContent);
    }

    [Fact]
    public void OpensTheMobileNavigationMenuOnDemand()
    {
        using var culture = new TestCulture("en");
        Services.Configure<AdGroupsOptions>(_ => { });
        Services.AddSingleton<HealthCheckService>(new HealthyHealthChecks());
        Services.AddSingleton(DeploymentManifest.Unknown);
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<InternalLayout>();
        var toggle = cut.Find("button.internal-nav-toggle");

        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Contains("Menu: Administration", toggle.TextContent);
        toggle.Click();

        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.Contains("Close menu", toggle.TextContent);
        Assert.Contains("open", cut.Find("nav#internal-navigation").ClassList);
    }

    private sealed class HealthyHealthChecks : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool>? predicate = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero));
    }
}