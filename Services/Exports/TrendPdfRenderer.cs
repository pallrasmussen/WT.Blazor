using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WT.Blazor.Localization;

namespace WT.Blazor.Services.Exports;

public sealed class TrendPdfRenderer
{
    public ExportFile Render(TrendReport report, int waitingTimeId, DateTime generatedAt, int? periodDays)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(style => style.FontSize(10));
                page.Header().Column(column =>
                {
                    column.Item().Text(AppText.Get(AppTextKey.TrendHeading)).FontSize(20).SemiBold();
                    column.Item().Text(report.Name).FontSize(14).SemiBold();
                    var periodLabel = AppText.Get(periodDays switch { 90 => "TrendPeriodThreeMonths", 365 => "TrendPeriodYear", _ => "TrendPeriodAll" });
                    column.Item().Text($"{AppText.Get("TrendPeriod")}: {periodLabel}");
                    column.Item().Text(report.Department is null ? report.Hospital : $"{report.Hospital} · {report.Department}");
                    column.Item().PaddingTop(6).Text($"{AppText.Get(AppTextKey.Current)}: {report.CurrentWait}").SemiBold();
                });
                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Item().Text(AppText.Get("ReferralEstimateDisclaimer"));
                    if (report.IsExpired) column.Item().Text(AppText.Get("EstimateExpiredContact"));
                    if (report.IsRange) column.Item().Text(AppText.Get("RangeTrendExplanation"));
                    if (report.ReferralDefinition is not null) column.Item().Text($"{AppText.Get("ReferralEndpoint")}: {report.ReferralDefinition}");
                    if (report.VerifiedAt is { } verified) column.Item().Text($"{AppText.Get("LastVerified")}: {verified:dd.MM.yyyy}");
                    if (!string.IsNullOrWhiteSpace(report.Conditions)) column.Item().Text($"{AppText.Get("ReferralConditions")}: {report.Conditions}");
                    column.Item().Text(AppText.Get("TrendSeriesExplanation"));
                    column.Item().Text(AppText.Get(AppTextKey.TrendHistory)).FontSize(13).SemiBold();
                    column.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Teal.Darken2).Padding(5).Text(AppText.Get(AppTextKey.Date)).FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Teal.Darken2).Padding(5).Text(AppText.Get(AppTextKey.Wait)).FontColor(Colors.White).SemiBold();
                        });
                        foreach (var point in report.Points)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(point.Timestamp.ToLocalTime().ToString("dd.MM.yyyy"));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{point.Value} {report.UnitLabel}");
                        }
                    });
                });
                page.Footer().AlignCenter().Text($"{AppText.Get(AppTextKey.ExportedAt)}: {generatedAt:g}");
            });
        });
        var periodSuffix = periodDays is null ? "" : $"_{periodDays}d";
        return ExportFile.FromWriter("application/pdf", $"{AppText.Get(AppTextKey.TrendExportFileName)}_{waitingTimeId}{periodSuffix}.pdf", (stream, token) =>
        {
            token.ThrowIfCancellationRequested();
            document.GeneratePdf(stream);
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
    }
}
