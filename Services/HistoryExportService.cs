using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Options;
using WT.Blazor.Localization;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public interface IHistoryExportService
{
    Task<ExportFile> CreateCsvAsync(HistoryQuery query, CancellationToken cancellationToken = default);
    Task<ExportFile> CreateExcelAsync(HistoryQuery query, CancellationToken cancellationToken = default);
    Task<ExportFile> CreatePdfAsync(HistoryQuery query, CancellationToken cancellationToken = default);
}

public sealed class HistoryExportService(
    IHistoryReadService historyReader,
    TimeProvider timeProvider,
    IOptions<ExportOptions> exportOptions) : IHistoryExportService
{
    public async Task<ExportFile> CreateCsvAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await GetRowsAsync(query, cancellationToken);
        static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
        var content = string.Join(',', HistoryExportSchema.Columns.Select(column => column.Header)) + "\r\n"
            + string.Join("\r\n", rows.Select(row => string.Join(',', HistoryExportSchema.Columns.Select(column => Csv(column.Value(row))))));
        return new ExportFile(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(content)).ToArray(), "text/csv", $"{AppText.Get(AppTextKey.HistoryExportFileName)}.csv");
    }

    public async Task<ExportFile> CreateExcelAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await GetRowsAsync(query, cancellationToken);
        static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
        var headers = string.Join("", HistoryExportSchema.Columns.Select(column => $"<th>{Encode(column.Header)}</th>"));
        var body = string.Join("", rows.Select(row => $"<tr>{string.Join("", HistoryExportSchema.Columns.Select(column => $"<td>{Encode(column.Value(row))}</td>"))}</tr>"));
        var content = $"""
            <!doctype html><html><head><meta charset="utf-8"></head><body><table border="1"><thead><tr>
            {headers}
            </tr></thead><tbody>{body}</tbody></table></body></html>
            """;
        return new ExportFile(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(content)).ToArray(), "application/vnd.ms-excel", $"{AppText.Get(AppTextKey.HistoryExportFileName)}.xls");
    }

    public async Task<ExportFile> CreatePdfAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        var rows = await GetRowsAsync(query, cancellationToken);
        var generatedAt = timeProvider.GetLocalNow().DateTime;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(style => style.FontSize(7));
                page.Header().Column(column =>
                {
                    column.Item().Text(AppText.Get(AppTextKey.TrendHistory)).FontSize(16).SemiBold();
                    column.Item().Text($"{AppText.Get(AppTextKey.ExportedAt)}: {generatedAt:g} · {AppText.Format(AppTextKey.ExportRecords, rows.Count)}");
                });
                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var column in HistoryExportSchema.Columns)
                            columns.RelativeColumn(column.PdfWidth);
                    });
                    table.Header(header =>
                    {
                        void HeaderCell(string text) => header.Cell().Background(Colors.Teal.Darken2).Padding(3).Text(text).FontColor(Colors.White).SemiBold();
                        foreach (var column in HistoryExportSchema.Columns)
                            HeaderCell(column.Header);
                    });
                    foreach (var row in rows)
                    {
                        void Cell(string text) => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(text);
                        foreach (var column in HistoryExportSchema.Columns)
                            Cell(column.Value(row));
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"{AppText.Get(AppTextKey.Page)} ");
                    text.CurrentPageNumber();
                    text.Span($" {AppText.Get(AppTextKey.Of)} ");
                    text.TotalPages();
                });
            });
        });
        return ExportFile.FromWriter("application/pdf", $"{AppText.Get(AppTextKey.HistoryExportPdfFileName)}.pdf", (stream, token) =>
        {
            token.ThrowIfCancellationRequested();
            document.GeneratePdf(stream);
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
    }

    private async Task<List<HistoryExportRow>> GetRowsAsync(HistoryQuery query, CancellationToken cancellationToken)
    {
        var maximumRows = exportOptions.Value.MaximumHistoryRows;
        if (maximumRows < 1) throw new InvalidOperationException("Exports:MaximumHistoryRows must be greater than zero.");
        var page = await historyReader.GetHistoryPageAsync(query with { PageNumber = 1, PageSize = maximumRows + 1 }, cancellationToken);
        if (page.TotalCount > maximumRows) throw new HistoryExportLimitExceededException(maximumRows);
        return page.Entries.Select(HistoryExportRow.From).ToList();
    }
}