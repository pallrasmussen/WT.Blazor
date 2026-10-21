using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HomePageTests : BunitContext
{
    [Fact]
    public void PublicPageHidesHospitalFilterAndLoadsAllWaitingTimes()
    {
        var catalog = new RecordingCatalogService();
        Services.AddSingleton<IWaitingTimeCatalogService>(catalog);
        Services.AddSingleton<ITrendReadService>(new EmptyTrendReadService());
        Services.AddSingleton<IPortalSettingsService>(new DefaultPortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<PortalTelemetry>();
        Services.AddSingleton(TimeProvider.System);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("/?sort=wait-desc&updated=stale");
        var cut = Render<Home>();

        Assert.Empty(cut.FindAll("#hospital"));
        Assert.Empty(cut.FindAll("#sort"));
        Assert.Empty(cut.FindAll("#updated"));
        Assert.Empty(cut.FindAll(".filter-reset"));
        Assert.NotNull(cut.Find("#search"));

        var request = catalog.Requests[^1];
        Assert.Null(request.Hospital);
        Assert.Null(request.Department);
    }

    [Fact]
    public void PublicPageShowsShortcutsAndFiltersBySelectedHospital()
    {
        var catalog = new RecordingCatalogService();
        Services.AddSingleton<IWaitingTimeCatalogService>(catalog);
        Services.AddSingleton<ITrendReadService>(new EmptyTrendReadService());
        Services.AddSingleton<IPortalSettingsService>(new DefaultPortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<PortalTelemetry>();
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<Home>();

        Assert.Equal(5, cut.FindAll(".filter-presets button").Count);
        Assert.DoesNotContain(cut.FindAll(".filter-presets button"),
            button => button.TextContent == "Krevur eftirlit");
        cut.FindAll(".filter-presets button")
            .Single(button => button.TextContent == DisplayNames.For(Hospital.Klaksvikar))
            .Click();

        var request = catalog.Requests[^1];
        Assert.Equal(Hospital.Klaksvikar, request.Hospital);
        Assert.Null(request.Department);
    }

    [Fact]
    public void PublicPageProvidesAnOverlayIndicatorLegend()
    {
        Services.AddSingleton<IWaitingTimeCatalogService>(new PopulatedCatalogService());
        Services.AddSingleton<ITrendReadService>(new EmptyTrendReadService());
        Services.AddSingleton<IPortalSettingsService>(new DefaultPortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<PortalTelemetry>();
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<Home>();

        Assert.Equal("0", cut.Find(".public-legend").GetAttribute("tabindex"));
        Assert.Equal("public-indicator-legend", cut.Find(".public-legend").GetAttribute("aria-describedby"));
        Assert.Equal("tooltip", cut.Find(".public-legend-tooltip").GetAttribute("role"));
        Assert.Single(cut.FindAll(".public-legend-tooltip .trend-legend"));
        Assert.Single(cut.FindAll(".public-legend-tooltip .freshness-legend"));
    }

    private sealed class RecordingCatalogService : IWaitingTimeCatalogService
    {
        public List<(Hospital? Hospital, Department? Department)> Requests { get; } = [];

        public Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department)
        {
            Requests.Add((hospital, department));
            return Task.FromResult(new List<WaitingTime>());
        }

        public Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department) =>
            Task.FromResult(new List<WaitingTime>());
    }

    private sealed class PopulatedCatalogService : IWaitingTimeCatalogService
    {
        private static readonly List<WaitingTime> WaitingTimes =
        [
            new()
            {
                Id = 1,
                Name = "Test waiting time",
                Hospital = Hospital.Klaksvikar,
                WaitValue = 2,
                Unit = WaitUnit.Vikur
            }
        ];

        public Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department) => Task.FromResult(WaitingTimes);
        public Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department) => Task.FromResult(WaitingTimes);
    }

    private sealed class EmptyTrendReadService : ITrendReadService
    {
        public Task<WaitingTime?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<WaitingTime?>(null);
        public Task<List<TrendPoint>> GetTrendPointsAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null) => Task.FromResult(new List<TrendPoint>());
        public Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds) => Task.FromResult(new Dictionary<int, int>());
    }

    private sealed class DefaultPortalSettingsService : IPortalSettingsService
    {
        public Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PortalSettings());
        public Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortalSettingsAuditEntry>>([]);
    }
}