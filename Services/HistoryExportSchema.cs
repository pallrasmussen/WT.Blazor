namespace WT.Blazor.Services;

public record HistoryExportColumn(string Header, float PdfWidth, Func<HistoryExportRow, string> Value);

public static class HistoryExportSchema
{
    public static IReadOnlyList<HistoryExportColumn> Columns { get; } =
    [
        new("Dagfesting og tíð", 1.1f, row => row.Timestamp),
        new("Sjúkrahús", 1.1f, row => row.Hospital),
        new("Depil", 1.1f, row => row.Department),
        new("Sergrein / viðgerð", 1.3f, row => row.Name),
        new("Gerð", .8f, row => row.Action),
        new("Broyting", 1.3f, row => row.Detail),
        new("Gamla bíðitíð", .85f, row => row.OldWait),
        new("Nýggja bíðitíð", .85f, row => row.NewWait),
        new("HN-brúkari", .9f, row => row.Username),
        new("Request ID", 1f, row => row.RequestId)
    ];
}