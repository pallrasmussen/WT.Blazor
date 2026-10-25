using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HomeRefreshTests : BunitContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObsoleteFilterResultsAndFailuresCannotReplaceTheLatestSelection(bool failOldRequest)
    {
        var catalog = new DelayedCatalog();
        Configure(catalog, new EmptyTrends(), TimeProvider.System);
        var cut = Render<Home>();
        var older = Preset(cut, Hospital.Klaksvikar).ClickAsync(new MouseEventArgs());
        var newer = Preset(cut, Hospital.Suduroyar).ClickAsync(new MouseEventArgs());
        catalog.Suduroy.SetResult([Item(2, "Latest result")]);
        await newer;
        cut.WaitForAssertion(() => Assert.Contains("Latest result", cut.Markup));
        if (failOldRequest) catalog.Klaksvik.SetException(new InvalidOperationException("Obsolete failure"));
        else catalog.Klaksvik.SetResult([Item(1, "Obsolete result")]);
        await older;
        Assert.Contains("Latest result", cut.Markup);
        Assert.DoesNotContain("Obsolete result", cut.Markup);
        Assert.Empty(cut.FindAll("[role='alert']"));
    }

    [Fact]
    public async Task OlderTrendCompletionCannotClearTheNewerRequestsLoadingState()
    {
        var catalog = new DelayedCatalog();
        var trends = new DelayedTrends();
        Configure(catalog, trends, TimeProvider.System);
        var cut = Render<Home>();
        var older = Preset(cut, Hospital.Klaksvikar).ClickAsync(new MouseEventArgs());
        catalog.Klaksvik.SetResult([Item(1, "Obsolete result")]);
        await trends.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var newer = Preset(cut, Hospital.Suduroyar).ClickAsync(new MouseEventArgs());
        trends.Release.SetResult(new Dictionary<int, int> { [1] = 99 });
        await older;
        var obsoleteWasShown = cut.Markup.Contains("Obsolete result", StringComparison.Ordinal);
        catalog.Suduroy.SetResult([Item(2, "Latest result")]);
        await newer;
        Assert.False(obsoleteWasShown);
        cut.WaitForAssertion(() => Assert.Contains("Latest result", cut.Markup));
        Assert.Empty(cut.FindAll(".waiting-card .trend-arrow"));
    }

    [Fact]
    public void SettingsRefreshPreservesFiltersAndSearchAndRetainsResultsAfterFailure()
    {
        var catalog = new MutableCatalog();
        Configure(catalog, new EmptyTrends(), TimeProvider.System);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/?hospital=Klaksvikar&q=Matching");
        var cut = Render<Home>();
        catalog.Fail = true;
        Services.GetRequiredService<PortalSettingsChangeNotifier>().NotifyChanged();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role='alert']")));
        Assert.Contains("Matching before", cut.Markup);
        catalog.Fail = false;
        catalog.Name = "Matching after";
        Services.GetRequiredService<PortalSettingsChangeNotifier>().NotifyChanged();
        cut.WaitForAssertion(() => Assert.Contains("Matching after", cut.Markup));
        Assert.DoesNotContain("Matching before", cut.Markup);
        Assert.Equal("Matching", cut.Find("#search").GetAttribute("value"));
        Assert.All(catalog.Requests, hospital => Assert.Equal(Hospital.Klaksvikar, hospital));
        Assert.Empty(cut.FindAll("[role='alert']"));
        catalog.Name = "Matching notified";
        Services.GetRequiredService<PortalSettingsChangeNotifier>().NotifyChanged();
        cut.WaitForAssertion(() => Assert.Contains("Matching notified", cut.Markup));
        Assert.Equal("Matching", cut.Find("#search").GetAttribute("value"));
    }

    private void Configure(IWaitingTimeCatalogService catalog, ITrendReadService trends, TimeProvider clock)
    {
        Services.AddSingleton(catalog);
        Services.AddSingleton(trends);
        Services.AddSingleton<IPortalSettingsService>(new DefaultSettings());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<PortalTelemetry>();
        Services.AddSingleton(clock);
    }

    private static AngleSharp.Dom.IElement Preset(IRenderedComponent<Home> cut, Hospital hospital) =>
        cut.FindAll(".filter-presets button").Single(button => button.TextContent == DisplayNames.For(hospital));

    private static WaitingTime Item(int id, string name) => new() { Id = id, Name = name, Hospital = Hospital.Klaksvikar, WaitValue = 2, Unit = WaitUnit.Vikur };

    private sealed class DelayedCatalog : IWaitingTimeCatalogService
    {
        public TaskCompletionSource<List<WaitingTime>> Klaksvik { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<List<WaitingTime>> Suduroy { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department) => hospital switch
        {
            Hospital.Klaksvikar => Klaksvik.Task,
            Hospital.Suduroyar => Suduroy.Task,
            _ => Task.FromResult(new List<WaitingTime>())
        };
        public Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department) => GetActiveAsync(hospital, department);
    }

    private sealed class MutableCatalog : IWaitingTimeCatalogService
    {
        public string Name { get; set; } = "Matching before";
        public bool Fail { get; set; }
        public List<Hospital?> Requests { get; } = [];
        public Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department)
        {
            Requests.Add(hospital);
            return Fail ? Task.FromException<List<WaitingTime>>(new InvalidOperationException("Load failed")) : Task.FromResult(new List<WaitingTime> { Item(1, Name) });
        }
        public Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department) => GetActiveAsync(hospital, department);
    }

    private class EmptyTrends : ITrendReadService
    {
        public Task<WaitingTime?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<WaitingTime?>(null);
        public Task<List<TrendPoint>> GetTrendPointsAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null) => Task.FromResult(new List<TrendPoint>());
        public virtual Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds) => Task.FromResult(new Dictionary<int, int>());
    }

    private sealed class DelayedTrends : EmptyTrends
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Dictionary<int, int>> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds)
        {
            if (!waitingTimeIds.Contains(1)) return base.GetTrendChangesAsync(waitingTimeIds);
            Started.TrySetResult();
            return Release.Task;
        }
    }

    private sealed class DefaultSettings : IPortalSettingsService
    {
        public Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PortalSettings());
        public Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortalSettingsAuditEntry>>([]);
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
