namespace WT.Blazor.Models;

public static class ReferralEstimate
{
    public static DateTime ReviewDate(WaitingTime item) => item.VerifiedAt
        ?? (item.EstimateStatus == EstimateStatus.Available ? DateTime.MinValue : item.UpdatedAt);
    public static bool IsExpired(WaitingTime item, PortalSettings settings, DateTime now) =>
        item.EstimateStatus == EstimateStatus.Available
        && (item.VerifiedAt is not { } verified || verified > now || verified <= UpdatedStatus.ReviewCutoff(now, settings.EstimateExpiryMonths));
}
