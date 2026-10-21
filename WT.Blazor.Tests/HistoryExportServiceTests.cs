using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HistoryExportServiceTests
{
    public HistoryExportServiceTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public async Task CreateCsvAsyncReturnsEscapedAuditData()
    {
        var reader = new StubHistoryReader(new WaitingTimeHistoryEntry
        {
            Timestamp = new DateTime(2026, 9, 27, 12, 34, 0, DateTimeKind.Utc),
            Hospital = Hospital.Klaksvikar,
            Name = "MR, \"skanning\"",
            Action = HistoryAction.Created,
            RequestId = "test-request-45"
        });
        var service = new HistoryExportService(reader, TimeProvider.System, Options.Create(new ExportOptions()));

        var export = await service.CreateCsvAsync(new HistoryQuery());
        var content = Encoding.UTF8.GetString(await export.ReadContentAsync());

        Assert.Equal("text/csv", export.ContentType);
        Assert.EndsWith(".csv", export.FileName);
        Assert.Contains("\"MR, \"\"skanning\"\"\"", content);
        Assert.Contains("\"test-request-45\"", content);
    }

    [Fact]
    public async Task CreateCsvAsyncNeutralizesSpreadsheetFormulasInUntrustedText()
    {
        var reader = new StubHistoryReader(new WaitingTimeHistoryEntry
        {
            Name = "=1+1",
            Detail = "+1+1",
            Username = "-1+1",
            RequestId = "@SUM(1,1)"
        });
        var service = new HistoryExportService(reader, TimeProvider.System, Options.Create(new ExportOptions()));

        var export = await service.CreateCsvAsync(new HistoryQuery());
        var content = Encoding.UTF8.GetString(await export.ReadContentAsync());

        Assert.Contains("\"'=1+1\"", content);
        Assert.Contains("\"'+1+1\"", content);
        Assert.Contains("\"'-1+1\"", content);
        Assert.Contains("\"'@SUM(1,1)\"", content);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(1,1)")]
    [InlineData(" =1+1")]
    [InlineData("\t=1+1")]
    public async Task CreateCsvAsyncNeutralizesFormulasAfterLeadingWhitespace(string value)
    {
        var reader = new StubHistoryReader(new WaitingTimeHistoryEntry { Name = value });
        var service = new HistoryExportService(reader, TimeProvider.System, Options.Create(new ExportOptions()));

        var export = await service.CreateCsvAsync(new HistoryQuery());
        var content = Encoding.UTF8.GetString(await export.ReadContentAsync());

        Assert.Contains($"\"'{value}\"", content);
    }

    [Fact]
    public async Task CreateExcelAsyncIncludesAllAuditColumns()
    {
        var reader = new StubHistoryReader(new WaitingTimeHistoryEntry
        {
            Timestamp = new DateTime(2026, 9, 27, 12, 34, 0, DateTimeKind.Utc),
            Hospital = Hospital.Klaksvikar,
            Name = "MR-skanning",
            Action = HistoryAction.Created,
            RequestId = "test-request-46"
        });
        var service = new HistoryExportService(reader, TimeProvider.System, Options.Create(new ExportOptions()));

        var export = await service.CreateExcelAsync(new HistoryQuery());
        var content = Encoding.UTF8.GetString(await export.ReadContentAsync());

        Assert.Contains($"<th>{System.Net.WebUtility.HtmlEncode("Request ID")}</th>", content);
        Assert.Contains("<td>test-request-46</td>", content);
    }

    [Fact]
    public async Task CreateCsvAsyncRejectsExportsAboveTheRowLimit()
    {
        var service = new HistoryExportService(
            new StubHistoryReader(totalCount: ExportOptions.DefaultMaximumHistoryRows + 1),
            TimeProvider.System,
            Options.Create(new ExportOptions()));

        await Assert.ThrowsAsync<HistoryExportLimitExceededException>(() => service.CreateCsvAsync(new HistoryQuery()));
    }

    [Fact]
    public async Task CreateTrendPdfAsyncReturnsAFileForAnExistingWaitingTime()
    {
        var item = new WaitingTime { Id = 42, Name = "MR-skanning", Hospital = Hospital.Klaksvikar, WaitValue = 3, Unit = WaitUnit.Vikur };
        var reportService = new StubTrendReportService(new TrendReport(
            item.Id,
            item.Name,
            DisplayNames.For(item.Hospital),
            null,
            $"{item.WaitValue} {DisplayNames.For(item.Unit, item.WaitValue)}",
            DisplayNames.For(item.Unit, item.WaitValue),
            [new TrendPoint(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 2)]));
        var service = new TrendExportService(reportService, TimeProvider.System, Options.Create(new ExportOptions()), NullLogger<TrendExportService>.Instance);

        var export = await service.CreatePdfAsync(item.Id);

        Assert.NotNull(export);
        Assert.Equal("application/pdf", export.ContentType);
        Assert.EndsWith("_42.pdf", export.FileName);
        var content = await export.ReadContentAsync();
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(content, 0, 4));
    }

    [Fact]
    public async Task CreateTrendPdfAsyncRejectsReportsAboveThePointLimit()
    {
        var reportService = new StubTrendReportService(new TrendReport(
            42, "MR-skanning", "Hospital", null, "3 vikur", "vikur",
            [new TrendPoint(new DateTime(2026, 9, 1), 2), new TrendPoint(new DateTime(2026, 9, 2), 3)]));
        var service = new TrendExportService(
            reportService,
            TimeProvider.System,
            Options.Create(new ExportOptions { MaximumTrendPoints = 1 }),
            NullLogger<TrendExportService>.Instance);

        await Assert.ThrowsAsync<TrendExportLimitExceededException>(() => service.CreatePdfAsync(42));
    }

    [Fact]
    public async Task TrendExportPassesCancellationToTheReportService()
    {
        var reportService = new StubTrendReportService(new TrendReport(
            42, "MR-skanning", "Hospital", null, "3 vikur", "vikur", []));
        var service = new TrendExportService(reportService, TimeProvider.System, Options.Create(new ExportOptions()), NullLogger<TrendExportService>.Instance);
        using var source = new CancellationTokenSource();

        await service.CreatePdfAsync(42, source.Token);

        Assert.Equal(source.Token, reportService.LastCancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(90)]
    [InlineData(365)]
    public async Task TrendExportPassesTheSelectedPeriodToTheReportService(int? period)
    {
        var reports = new StubTrendReportService(new TrendReport(42, "Record", "Hospital", null, "2 days", "days", []));
        var service = new TrendExportService(reports, TimeProvider.System, Options.Create(new ExportOptions()), NullLogger<TrendExportService>.Instance);
        var file = await service.CreatePdfAsync(42, periodDays: period);
        Assert.NotNull(file);
        Assert.Equal(period, reports.LastPeriodDays);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreatePdfAsync(42, periodDays: 1));
    }

    [Fact]
    public async Task TrendReportProjectsDisplayDataAndPoints()
    {
        var item = new WaitingTime
        {
            Id = 42,
            Name = "MR-skanning",
            Hospital = Hospital.Landssjukrahusid,
            Department = Department.Medisinskidepilin,
            WaitValue = 3,
            Unit = WaitUnit.Vikur
        };
        var points = new List<TrendPoint> { new(new DateTime(2026, 9, 1), 2) };
        var reader = new StubTrendReader(item: item, points: points);
        var service = new TrendReportService(reader);

        using var source = new CancellationTokenSource();
        var report = await service.GetAsync(item.Id, source.Token, periodDays: 90);

        Assert.NotNull(report);
        Assert.Equal(item.Name, report.Name);
        Assert.Equal(DisplayNames.For(item.Hospital), report.Hospital);
        Assert.Equal(DisplayNames.For(item.Department.Value), report.Department);
        Assert.Equal($"3 {DisplayNames.For(item.Unit, 3)}", report.CurrentWait);
        Assert.Same(points, report.Points);
        Assert.Equal(source.Token, reader.LastCancellationToken);
        Assert.Equal(90, reader.LastPeriodDays);
    }

    private sealed class StubHistoryReader(
        WaitingTimeHistoryEntry? entry = null,
        int? totalCount = null) : IHistoryReadService
    {
        public Task<List<WaitingTimeHistoryEntry>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default) => Task.FromResult(entry is null ? [] : new List<WaitingTimeHistoryEntry> { entry });
        public Task<HistoryPage> GetHistoryPageAsync(HistoryQuery query, CancellationToken cancellationToken = default)
        {
            var entries = entry is null ? [] : new List<WaitingTimeHistoryEntry> { entry };
            return Task.FromResult(new HistoryPage(entries, totalCount ?? entries.Count));
        }
        public Task<List<string>> GetHistoryNamesAsync() => throw new NotSupportedException();
        public Task<List<string>> GetHistoryUsersAsync() => throw new NotSupportedException();
        public Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds) => throw new NotSupportedException();
    }

    private sealed class StubTrendReader(
        WaitingTime? item = null,
        List<TrendPoint>? points = null) : ITrendReadService
    {
        public CancellationToken LastCancellationToken { get; private set; }
        public int? LastPeriodDays { get; private set; }
        public Task<WaitingTime?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(item);
        }
        public Task<List<TrendPoint>> GetTrendPointsAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
        {
            LastCancellationToken = cancellationToken;
            LastPeriodDays = periodDays;
            return Task.FromResult(points ?? []);
        }
        public Task<Dictionary<int, int>> GetTrendChangesAsync(IEnumerable<int> waitingTimeIds) => throw new NotSupportedException();
    }

    private sealed class StubTrendReportService(TrendReport? report) : ITrendReportService
    {
        public CancellationToken LastCancellationToken { get; private set; }
        public int? LastPeriodDays { get; private set; }
        public Task<TrendReport?> GetAsync(int waitingTimeId, CancellationToken cancellationToken = default, int? periodDays = null)
        {
            LastCancellationToken = cancellationToken;
            LastPeriodDays = periodDays;
            return Task.FromResult(report);
        }
    }
}
