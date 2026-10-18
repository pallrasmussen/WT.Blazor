namespace WT.Blazor.Models;

// Faroese: gives a quick "how stale is this?" visual indicator (green/yellow/red dot).
public static class UpdatedStatus
{
    public static DateTime ReviewCutoff(DateTime nowUtc, int greenMaximumAgeMonths = PortalSettings.DefaultGreenMaximumAgeMonths) =>
        nowUtc.AddMonths(-greenMaximumAgeMonths);

    public static bool RequiresReview(DateTime updatedAtUtc, DateTime? nowUtc = null) =>
        updatedAtUtc <= ReviewCutoff(nowUtc ?? DateTime.UtcNow);

    public static bool RequiresReview(DateTime updatedAtUtc, int greenMaximumAgeMonths, DateTime? nowUtc = null) =>
        updatedAtUtc <= ReviewCutoff(nowUtc ?? DateTime.UtcNow, greenMaximumAgeMonths);

    public static string CssClass(DateTime updatedAtUtc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        return CssClass(updatedAtUtc, PortalSettings.DefaultGreenMaximumAgeMonths, PortalSettings.DefaultYellowMaximumAgeMonths, now);
    }

    public static string CssClass(DateTime updatedAtUtc, int greenMaximumAgeMonths, int yellowMaximumAgeMonths, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var greenCutoff = ReviewCutoff(now, greenMaximumAgeMonths);
        var yellowCutoff = now.AddMonths(-yellowMaximumAgeMonths);

        if (updatedAtUtc > greenCutoff) return "status-fresh";
        if (updatedAtUtc > yellowCutoff) return "status-warning";
        return "status-old";
    }
}
