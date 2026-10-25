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
    public void ChartUsesStraightSegmentsAndExtendsTheLatestEstimateToToday()
    {
        var cut = Render<WaitTrendChart>(parameters => parameters
            .Add(component => component.Points,
            [
                new TrendPoint(new DateTime(2020, 1, 1), 3),
                new TrendPoint(new DateTime(2020, 2, 1), 4)
            ])
            .Add(component => component.UnitLabel, "weeks"));

        var paths = cut.FindAll("path");

        Assert.Contains(paths, path => path.GetAttribute("d")!.Contains(" L "));
        Assert.Single(cut.FindAll("path.chart-current-estimate"));
    }

    [Fact]
    public void ChartHidesLatestRecordedDateWhenItWouldOverlapTheCurrentLabel()
    {
        var latestDate = DateTime.UtcNow.Date.AddDays(-1);
        var cut = Render<WaitTrendChart>(parameters => parameters
            .Add(component => component.Points,
            [
                new TrendPoint(latestDate.AddMonths(-3), 3),
                new TrendPoint(latestDate, 4)
            ])
            .Add(component => component.UnitLabel, "weeks"));

        Assert.DoesNotContain(
            cut.FindAll("text.chart-date-label"),
            label => label.TextContent == latestDate.ToString("dd.MM.yy"));
    }

    [Fact]
    public void ChartAlignsWholeMonthValuesWithTheirYAxisGridLine()
    {
        var cut = Render<WaitTrendChart>(parameters => parameters
            .Add(component => component.Points,
            [
                new TrendPoint(new DateTime(2020, 1, 1), 3),
                new TrendPoint(new DateTime(2020, 2, 1), 1)
            ])
            .Add(component => component.UnitLabel, "mánaðir"));

        var valueLabel = cut.FindAll("text").Single(text => text.TextContent == "3");
        var point = cut.FindAll("circle.chart-point").First();
        var valueLabelY = double.Parse(valueLabel.GetAttribute("y")!, System.Globalization.CultureInfo.InvariantCulture);
        var pointY = double.Parse(point.GetAttribute("cy")!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(valueLabelY - 4, pointY, precision: 8);
    }

    [Fact]
    public void ChartHidesDateLabelsThatWouldOverlapTheLatestDate()
    {
        var cut = Render<WaitTrendChart>(parameters => parameters
            .Add(component => component.Points,
            [
                new TrendPoint(new DateTime(2030, 5, 15), 35),
                new TrendPoint(new DateTime(2030, 6, 15), 34),
                new TrendPoint(new DateTime(2030, 8, 15), 32),
                new TrendPoint(new DateTime(2030, 9, 15), 31),
                new TrendPoint(new DateTime(2030, 10, 15), 36),
                new TrendPoint(new DateTime(2030, 10, 16), 38)
            ])
            .Add(component => component.UnitLabel, "vikur"));

        var dateLabels = cut.FindAll("text.chart-date-label");

        Assert.DoesNotContain(dateLabels, label => label.TextContent == "15.10.30");
        Assert.Contains(dateLabels, label => label.TextContent == "16.10.30");
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
    public void TrendPagePreservesSafeHashOnlyReturnUrls()
    {
        Services.AddSingleton<ITrendReportService>(new StubTrendReportService(null));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/gongd/42?returnUrl=%23waiting-time-42");

        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 42));

        cut.WaitForAssertion(() => Assert.Equal("/#waiting-time-42", cut.Find("a.trend-back").GetAttribute("href")));
    }

    [Fact]
    public void TrendPageFiltersPointsToSelectedPeriod()
    {
        var reportService = new StubTrendReportService(new TrendReport(
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
            ]));
        Services.AddSingleton<ITrendReportService>(reportService);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/gongd/7?returnUrl=%2F%3Fhospital%3DLandssj%C3%BAkrah%C3%BAsi%C3%B0");

        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 7));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("circle.chart-point").Count));
        Assert.Equal("/?hospital=Landssjúkrahúsið", cut.Find("a.trend-back").GetAttribute("href"));

        cut.Find("#trend-period").Change("90");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("circle.chart-point").Count));
        Assert.Equal([null, 90], reportService.RequestedPeriods);
    }

    private sealed class StubTrendReportService(TrendReport? report) : ITrendReportService
    {
        public List<int?> RequestedPeriods { get; } = [];

        public Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
        {
            RequestedPeriods.Add(periodDays);
            return Task.FromResult(report);
        }
    }
}