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
    public static string WaitText(WaitingTime item) => item.EstimateStatus == EstimateStatus.Available
        ? $"{item.WaitValue} {DisplayNames.For(item.Unit, item.WaitValue)}"
        : StatusLabel(item.EstimateStatus);
}
