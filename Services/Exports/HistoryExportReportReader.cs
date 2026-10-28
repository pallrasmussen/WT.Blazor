using Microsoft.Extensions.Options;
using WT.Blazor.Models;

namespace WT.Blazor.Services.Exports;

public sealed record HistoryExportReport(IReadOnlyList<HistoryExportRow> Rows, DateTime GeneratedAt);

public interface IHistoryExportReportReader
{
    Task<HistoryExportReport> GetAsync(HistoryQuery query, CancellationToken cancellationToken = default);
}

public sealed class HistoryExportReportReader(IHistoryReadService historyReader, TimeProvider timeProvider,
    IOptions<ExportOptions> exportOptions) : IHistoryExportReportReader
{
    public async Task<HistoryExportReport> GetAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        var maximumRows = exportOptions.Value.MaximumHistoryRows;
        if (maximumRows < 1) throw new InvalidOperationException("Exports:MaximumHistoryRows must be greater than zero.");
        var page = await historyReader.GetHistoryPageAsync(query with { PageNumber = 1, PageSize = maximumRows + 1 }, cancellationToken);
        if (page.TotalCount > maximumRows) throw new HistoryExportLimitExceededException(maximumRows);
        return new(page.Entries.Select(HistoryExportRow.From).ToList(), timeProvider.GetLocalNow().DateTime);
    }
}
