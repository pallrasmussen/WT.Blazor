namespace WT.Blazor.Services;

public sealed class TrendExportLimitExceededException(int maximumPoints)
    : InvalidOperationException($"The trend export contains more than {maximumPoints:N0} points. Narrow the date range and try again.");