using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using WT.Blazor.Components.Shared;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class TrendComponentTests : BunitContext
{
    [Fact]
    public void ChartShowsEmptyStateWhenThereAreFewerThanTwoPoints()
    {
        var cut = Render<WaitTrendChart>(parameters => parameters
            .Add(component => component.Points, [new TrendPoint(new DateTime(2026, 9, 1), 3)])
            .Add(component => component.UnitLabel, "vikur"));

        Assert.Empty(cut.FindAll("svg.chart-svg"));
        Assert.Single(cut.FindAll(".empty-state"));
    }

    [Fact]
    public void ChartRendersPointDataAndEscapesLabels()
    {
        var cut = Render<WaitTrendChart>(parameters => parameters
            .Add(component => component.Points,
            [
                new TrendPoint(new DateTime(2026, 9, 1), 3),
                new TrendPoint(new DateTime(2026, 9, 2), 4)
            ])
            .Add(component => component.UnitLabel, "<unsafe>"));

        Assert.Single(cut.FindAll("svg.chart-svg"));
        Assert.Equal(2, cut.FindAll("circle.chart-point").Count);
        Assert.Equal(2, cut.FindAll(".trend-data-table tbody tr").Count);
        Assert.DoesNotContain("<unsafe>", cut.Markup);
        Assert.Contains("&lt;unsafe&gt;", cut.Markup);
    }

    [Fact]
    public void TrendPageUsesOnlyLocalReturnUrlsAndShowsNotFoundState()
    {
        Services.AddSingleton<ITrendReportService>(new StubTrendReportService(null));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/gongd/42?returnUrl=https%3A%2F%2Funtrusted.example");

        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 42));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("/", cut.Find("a.trend-back").GetAttribute("href"));
            Assert.Single(cut.FindAll(".empty-state"));
        });
    }

    [Fact]
    public void TrendPageFiltersPointsToSelectedPeriod()
    {
        Services.AddSingleton<ITrendReportService>(new StubTrendReportService(new TrendReport(
            7,
            "MR",
            "Landssjúkrahúsið",
            null,
            "4 vikur",
            "vikur",
            [
                new TrendPoint(new DateTime(2025, 1, 1), 2),
                new TrendPoint(new DateTime(2026, 8, 1), 3),
                new TrendPoint(new DateTime(2026, 9, 1), 4)
            ])));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/gongd/7?returnUrl=%2F%3Fhospital%3DLandssj%C3%BAkrah%C3%BAsi%C3%B0");

        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 7));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("circle.chart-point").Count));
        Assert.Equal("/?hospital=Landssjúkrahúsið", cut.Find("a.trend-back").GetAttribute("href"));

        cut.Find("#trend-period").Change("90");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("circle.chart-point").Count));
    }

    private sealed class StubTrendReportService(TrendReport? report) : ITrendReportService
    {
        public Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default) => Task.FromResult(report);
    }
}