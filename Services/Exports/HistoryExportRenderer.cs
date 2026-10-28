using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WT.Blazor.Localization;

namespace WT.Blazor.Services.Exports;

public sealed class HistoryExportRenderer
{
    public ExportFile CreateCsv(HistoryExportReport report)
    {
        var rows = report.Rows;
        static string Csv(string value, bool mayContainUntrustedText)
        {
            var firstNonWhitespace = value.AsSpan().TrimStart();
            if (mayContainUntrustedText && !firstNonWhitespace.IsEmpty && firstNonWhitespace[0] is '=' or '+' or '-' or '@')
                value = $"'{value}";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        var content = string.Join(',', HistoryExportSchema.Columns.Select(column => column.Header)) + "\r\n"
            + string.Join("\r\n", rows.Select(row => string.Join(',', HistoryExportSchema.Columns.Select(column => Csv(column.Value(row), column.MayContainUntrustedText)))));
        return new ExportFile(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(content)).ToArray(), "text/csv", $"{AppText.Get(AppTextKey.HistoryExportFileName)}.csv");
    }

    public ExportFile CreateExcel(HistoryExportReport report)
    {
        var rows = report.Rows;
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

    public ExportFile CreatePdf(HistoryExportReport report)
    {
        var rows = report.Rows;
        var generatedAt = report.GeneratedAt;
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
}
