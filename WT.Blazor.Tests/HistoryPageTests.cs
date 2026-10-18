using Bunit;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HistoryPageTests : BunitContext
{
    [Fact]
    public void DisplaysTheLegacyDemoDetailAsDemo()
    {
        Services.AddSingleton<IHistoryReadService>(new LegacyDemoHistoryReader());
        Services.Configure<ExportOptions>(_ => { });
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<History>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(">Demo<", cut.Markup);
            Assert.DoesNotContain("Dømisøga til demo", cut.Markup);
        });
    }

    [Fact]
    public void FilteringAndPagingSendTheExpectedHistoryQueries()
    {
        var reader = new RecordingHistoryReader();
        Services.AddSingleton<IHistoryReadService>(reader);
        Services.Configure<ExportOptions>(_ => { });
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<History>();
        cut.WaitForAssertion(() => Assert.NotEmpty(reader.Queries));

        cut.Find("#history-hospital").Change(Hospital.Landssjukrahusid.ToString());
        cut.WaitForAssertion(() => Assert.Equal(Hospital.Landssjukrahusid, reader.Queries[^1].Hospital));

        cut.FindAll(".log-pager button")[1].Click();
        cut.WaitForAssertion(() => Assert.Equal(2, reader.Queries[^1].PageNumber));
    }

    [Fact]
    public void DisplayControlsToggleCompactModeAndOptionalColumns()
    {
        Services.AddSingleton<IHistoryReadService>(new LegacyDemoHistoryReader());
        Services.Configure<ExportOptions>(_ => { });
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<History>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("table.history-table")));

        cut.Find(".history-display-controls input[type='checkbox']").Change(true);
        Assert.Contains("compact", cut.Find("table.history-table").ClassName);

        cut.FindAll(".column-menu input[type='checkbox']")[0].Change(false);
        Assert.Empty(cut.FindAll(".history-hospital-column"));
    }

    private sealed class LegacyDemoHistoryReader : IHistoryReadService
    {
        private static readonly WaitingTimeHistoryEntry Entry = new()
        {
            Id = 1,
            WaitingTimeId = 1,
            Timestamp = new DateTime(2026, 9, 28),
            Hospital = Hospital.Klaksvikar,
            Name = "Demo waiting time",
            Action = HistoryAction.Updated,
            Detail = "Dømisøga til demo",
            Username = "demo"
        };

        public Task<List<WaitingTimeHistoryEntry>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new List<WaitingTimeHistoryEntry> { Entry });
        public Task<HistoryPage> GetHistoryPageAsync(HistoryQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new HistoryPage([Entry], 1));
        public Task<List<string>> GetHistoryNamesAsync() => Task.FromResult(new List<string> { Entry.Name });
        public Task<List<string>> GetHistoryUsersAsync() => Task.FromResult(new List<string> { Entry.Username! });
        public Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds) => Task.FromResult(new Dictionary<int, ReviewInfo>());
    }

    private sealed class RecordingHistoryReader : IHistoryReadService
    {
        public List<HistoryQuery> Queries { get; } = [];

        public Task<List<WaitingTimeHistoryEntry>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new List<WaitingTimeHistoryEntry>());

        public Task<HistoryPage> GetHistoryPageAsync(HistoryQuery query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(new HistoryPage([], 100));
        }

        public Task<List<string>> GetHistoryNamesAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetHistoryUsersAsync() => Task.FromResult(new List<string>());
        public Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds) => Task.FromResult(new Dictionary<int, ReviewInfo>());
    }
}