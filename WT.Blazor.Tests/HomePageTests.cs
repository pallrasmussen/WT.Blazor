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
    [Theory]
    [InlineData(EstimateStatus.Available)]
    [InlineData(EstimateStatus.Unknown)]
    [InlineData(EstimateStatus.ServiceUnavailable)]
    public void PublicCardsDistinguishUnavailableEstimatesFromZeroAndHideInternalOwnership(EstimateStatus status)
    {
        using var culture = new WT.Blazor.Tests.TestInfrastructure.TestCulture("en");
        var item = new WaitingTime
        {
            Id = 22,
            Name = "MRI",
            Hospital = Hospital.Klaksvikar,
            WaitValue = 0,
            Unit = WaitUnit.Dagar,
            EstimateStatus = status,
            ReferralEndpoint = ReferralEndpoint.Investigation,
            ResponsibleDepartment = "INTERNAL-OWNER",
            VerifiedAt = new DateTime(2026, 1, 1),
            Note = "Adults only"
        };
        Services.AddSingleton<IWaitingTimeCatalogService>(new SingleItemCatalog(item));
        Services.AddSingleton<ITrendReadService>(new EmptyTrendReadService());
        Services.AddSingleton<IPortalSettingsService>(new DefaultPortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<PortalTelemetry>();
        Services.AddSingleton(TimeProvider.System);
        var cut = Render<Home>();
        Assert.DoesNotContain("INTERNAL-OWNER", cut.Markup);
        Assert.Contains("Investigation", cut.Markup);
        Assert.Contains("routine referrals", cut.Markup);
        Assert.Contains("Adults only", cut.Markup);
        var card = cut.Find("#waiting-time-22");
        if (status == EstimateStatus.Available)
        {
            Assert.Contains("0", card.QuerySelector(".wait-value")!.TextContent);
            Assert.Contains("Last verified", card.TextContent);
            Assert.NotNull(card.QuerySelector(".graph-link"));
        }
        else
        {
            Assert.Equal(ReferralEstimate.StatusLabel(status), card.QuerySelector(".wait-value")!.TextContent.Trim());
            Assert.Null(card.QuerySelector(".graph-link"));
            Assert.DoesNotContain("Last verified", card.TextContent);
        }
    }

    [Fact]
    public void ExpiredRangesHideTheCurrentNumberAndGiveContactGuidance()
    {
        using var culture = new WT.Blazor.Tests.TestInfrastructure.TestCulture("en");
        var item = new WaitingTime { Id = 22, Name = "MRI", Hospital = Hospital.Klaksvikar, WaitValue = 6, WaitUpperValue = 10, Unit = WaitUnit.Vikur, VerifiedAt = DateTime.UtcNow.AddMonths(-13) };
        Services.AddSingleton<IWaitingTimeCatalogService>(new SingleItemCatalog(item));
        Services.AddSingleton<ITrendReadService>(new EmptyTrendReadService());
        Services.AddSingleton<IPortalSettingsService>(new DefaultPortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<PortalTelemetry>();
        Services.AddSingleton(TimeProvider.System);
        var cut = Render<Home>();
        var card = cut.Find("#waiting-time-22");
        Assert.Equal("Estimate expired", card.QuerySelector(".wait-value")!.TextContent.Trim());
        Assert.Contains("contact the relevant department", card.TextContent);
        Assert.DoesNotContain("6–10", card.TextContent);
        Assert.Empty(card.QuerySelectorAll(".trend-arrow"));
    }

    private sealed class SingleItemCatalog(WaitingTime item) : IWaitingTimeCatalogService
    {
        public Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department) => Task.FromResult(new List<WaitingTime> { item });
        public Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department) => GetActiveAsync(hospital, department);
    }

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

        Assert.Equal("DETAILS", cut.Find(".public-legend").TagName);
        Assert.NotNull(cut.Find(".public-legend summary"));
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