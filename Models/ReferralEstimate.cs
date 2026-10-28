using WT.Blazor.Localization;

namespace WT.Blazor.Models;

public enum ReferralEndpoint { Unspecified, FirstConsultation, Investigation, Treatment }
public enum EstimateStatus { Available, Unknown, ServiceUnavailable }

public static class ReferralEstimate
{
    public static DateTime ReviewDate(WaitingTime item) => item.VerifiedAt
        ?? (item.EstimateStatus == EstimateStatus.Available ? DateTime.MinValue : item.UpdatedAt);
    public static string EndpointLabel(ReferralEndpoint endpoint) => AppText.Get($"ReferralEndpoint{endpoint}");
    public static string StatusLabel(EstimateStatus status) => AppText.Get($"EstimateStatus{status}");
    public static bool IsExpired(WaitingTime item, PortalSettings settings, DateTime now) =>
        item.EstimateStatus == EstimateStatus.Available
        && (item.VerifiedAt is not { } verified || verified > now || verified <= UpdatedStatus.ReviewCutoff(now, settings.EstimateExpiryMonths));

    public static string NumericText(WaitingTime item, bool canonicalDays = false)
    {
        var lower = canonicalDays ? WaitDuration.ToDays(item.WaitValue, item.Unit) : item.WaitValue;
        var upper = item.WaitUpperValue is { } value ? canonicalDays ? WaitDuration.ToDays(value, item.Unit) : value : (int?)null;
        var unit = canonicalDays ? WaitUnit.Dagar : item.Unit;
        return $"{(upper is null ? lower.ToString() : $"{lower}–{upper}")} {DisplayNames.For(unit, upper ?? lower)}";
    }

    public static string PublicWaitText(WaitingTime item, PortalSettings settings, DateTime now) => IsExpired(item, settings, now)
        ? AppText.Get("EstimateExpired") : WaitText(item);

    public static string WaitText(WaitingTime item) => item.EstimateStatus == EstimateStatus.Available
        ? NumericText(item) : StatusLabel(item.EstimateStatus);
}
