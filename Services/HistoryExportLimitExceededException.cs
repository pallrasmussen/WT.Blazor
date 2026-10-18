namespace WT.Blazor.Services;

public sealed class HistoryExportLimitExceededException(int maximumRows)
    : InvalidOperationException($"The export contains more than {maximumRows:N0} rows. Narrow the filters and try again.");