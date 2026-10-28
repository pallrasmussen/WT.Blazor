using WT.Blazor.Services.Exports;

namespace WT.Blazor.Services;

public interface IHistoryExportService
{
    Task<ExportFile> CreateCsvAsync(HistoryQuery query, CancellationToken cancellationToken = default);
    Task<ExportFile> CreateExcelAsync(HistoryQuery query, CancellationToken cancellationToken = default);
    Task<ExportFile> CreatePdfAsync(HistoryQuery query, CancellationToken cancellationToken = default);
}

public sealed class HistoryExportService(IHistoryExportReportReader reportReader, HistoryExportRenderer renderer) : IHistoryExportService
{
    public async Task<ExportFile> CreateCsvAsync(HistoryQuery query, CancellationToken cancellationToken = default) =>
        renderer.CreateCsv(await reportReader.GetAsync(query, cancellationToken));

    public async Task<ExportFile> CreateExcelAsync(HistoryQuery query, CancellationToken cancellationToken = default) =>
        renderer.CreateExcel(await reportReader.GetAsync(query, cancellationToken));

    public async Task<ExportFile> CreatePdfAsync(HistoryQuery query, CancellationToken cancellationToken = default) =>
        renderer.CreatePdf(await reportReader.GetAsync(query, cancellationToken));
}
