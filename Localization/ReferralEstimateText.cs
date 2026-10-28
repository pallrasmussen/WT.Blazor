using WT.Blazor.Models;

namespace WT.Blazor.Localization;

public static class ReferralEstimateText
{
    public static string EndpointLabel(ReferralEndpoint endpoint) => AppText.Get($"ReferralEndpoint{endpoint}");
    public static string StatusLabel(EstimateStatus status) => AppText.Get($"EstimateStatus{status}");
    public static string NumericText(WaitingTime item, bool canonicalDays = false)
    {
        var lower = canonicalDays ? WaitDuration.ToDays(item.WaitValue, item.Unit) : item.WaitValue;
        var upper = item.WaitUpperValue is { } value ? canonicalDays ? WaitDuration.ToDays(value, item.Unit) : value : (int?)null;
        var unit = canonicalDays ? WaitUnit.Dagar : item.Unit;
        return $"{(upper is null ? lower.ToString() : $"{lower}–{upper}")} {DisplayNames.For(unit, upper ?? lower)}";
    }

    public static string PublicWaitText(WaitingTime item, PortalSettings settings, DateTime now) => ReferralEstimate.IsExpired(item, settings, now)
        ? AppText.Get("EstimateExpired") : WaitText(item);

    public static string WaitText(WaitingTime item) => item.EstimateStatus == EstimateStatus.Available
        ? NumericText(item) : StatusLabel(item.EstimateStatus);
}
