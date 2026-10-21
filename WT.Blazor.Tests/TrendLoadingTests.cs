using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class TrendLoadingTests : BunitContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderPeriodResultsAndErrorsCannotReplaceTheLatestPeriod(bool failOlder)
    {
        var service = new ControlledReports();
        Services.AddSingleton<ITrendReportService>(service);
        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 1));
        var older = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "90" });
        var newer = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "365" });
        service.Year.SetResult(Report(1, "Latest year"));
        await newer;
        if (failOlder) service.Quarter.SetException(new InvalidOperationException("Obsolete failure"));
        else service.Quarter.SetResult(Report(1, "Obsolete quarter"));
        await older;
        Assert.Contains("Latest year", cut.Markup);
        Assert.DoesNotContain("Obsolete quarter", cut.Markup);
        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.Equal("/api/trend/1/export/pdf?periodDays=365", cut.Find(".trend-actions a").GetAttribute("href"));
    }

    [Fact]
    public async Task OlderCompletionCannotClearLoadingOrExposeAnExportForTheWrongPeriod()
    {
        var service = new ControlledReports();
        Services.AddSingleton<ITrendReportService>(service);
        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 1));
        var older = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "90" });
        var newer = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "365" });
        service.Quarter.SetResult(Report(1, "Obsolete quarter"));
        await older;
        var exportWasHidden = cut.FindAll(".trend-actions a").Count == 0;
        var wasBusy = cut.Find("section.public-panel").GetAttribute("aria-busy");
        service.Year.SetResult(Report(1, "Latest year"));
        await newer;
        Assert.True(exportWasHidden);
        Assert.Equal("true", wasBusy);
        Assert.Equal("/api/trend/1/export/pdf?periodDays=365", cut.Find(".trend-actions a").GetAttribute("href"));
    }

    [Fact]
    public async Task NavigationDiscardsTheOldRecordAndResetsItsPeriod()
    {
        var service = new ControlledReports();
        Services.AddSingleton<ITrendReportService>(service);
        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 1));
        var old = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "90" });
        cut.Render(parameters => parameters.Add(component => component.Id, 2));
        service.Quarter.SetResult(Report(1, "Old record"));
        await old;
        Assert.Contains("Record 2", cut.Markup);
        Assert.DoesNotContain("Old record", cut.Markup);
        Assert.Equal("/api/trend/2/export/pdf", cut.Find(".trend-actions a").GetAttribute("href"));
    }

    [Fact]
    public void InitialFailureHasARetryActionAndDoesNotLookLikeANotFoundResult()
    {
        var service = new RetriableReports();
        Services.AddSingleton<ITrendReportService>(service);
        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 1));
        Assert.Single(cut.FindAll("[role='alert']"));
        Assert.Empty(cut.FindAll(".trend-actions a"));
        service.Fail = false;
        cut.Find(".retry-trend").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".trend-actions a")));
        Assert.Empty(cut.FindAll("[role='alert']"));
    }

    [Fact]
    public async Task FailedPeriodCanBeRetriedAndDisposalCancelsOutstandingWork()
    {
        var service = new ControlledReports();
        Services.AddSingleton<ITrendReportService>(service);
        var cut = Render<Trend>(parameters => parameters.Add(component => component.Id, 1));
        var failed = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "90" });
        service.Quarter.SetException(new InvalidOperationException("Temporary failure"));
        await failed;
        Assert.Single(cut.FindAll("[role='alert']"));
        service.Quarter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var retry = cut.Find(".retry-trend").ClickAsync(new MouseEventArgs());
        service.Quarter.SetResult(Report(1, "Retry result"));
        await retry;
        Assert.Equal("/api/trend/1/export/pdf?periodDays=90", cut.Find(".trend-actions a").GetAttribute("href"));
        var pending = cut.Find("#trend-period").ChangeAsync(new ChangeEventArgs { Value = "365" });
        await DisposeComponentsAsync();
        Assert.True(service.LastToken.IsCancellationRequested);
        service.Year.SetCanceled(service.LastToken);
        await pending;
    }

    private static TrendReport Report(int id, string name) => new(id, name, "Hospital", null, "2 days", "days", []);

    private sealed class RetriableReports : ITrendReportService
    {
        public bool Fail { get; set; } = true;
        public Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null) =>
            Fail ? Task.FromException<TrendReport?>(new InvalidOperationException("Temporary failure")) : Task.FromResult<TrendReport?>(Report(waitingTimeId, "Retry result"));
    }

    private sealed class ControlledReports : ITrendReportService
    {
        public TaskCompletionSource<TrendReport?> Quarter { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TrendReport?> Year { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken LastToken { get; private set; }
        public Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
        {
            LastToken = cancellationToken;
            return periodDays switch
            {
                90 => Quarter.Task,
                365 => Year.Task,
                _ => Task.FromResult<TrendReport?>(Report(waitingTimeId, $"Record {waitingTimeId}"))
            };
        }
    }
}
