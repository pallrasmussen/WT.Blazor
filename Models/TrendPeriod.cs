namespace WT.Blazor.Models;

public static class TrendPeriod
{
    public static bool IsSupported(int? days) => days is null or 90 or 365;
}
