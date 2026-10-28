namespace WT.Blazor.Models;

[Flags]
public enum EstimateReviewReason { None = 0, Overdue = 1, MissingVerification = 2, MissingOwner = 4, MissingEndpoint = 8 }

public static class EstimateReview
{
    public static EstimateReviewReason Reasons(WaitingTime item, int maximumAgeMonths, DateTime now)
    {
        var reasons = EstimateReviewReason.None;
        if (item.EstimateStatus == EstimateStatus.Available && (item.VerifiedAt is null || item.VerifiedAt > now)) reasons |= EstimateReviewReason.MissingVerification;
        else if (UpdatedStatus.RequiresReview(ReferralEstimate.ReviewDate(item), maximumAgeMonths, now)) reasons |= EstimateReviewReason.Overdue;
        if (string.IsNullOrWhiteSpace(item.ResponsibleDepartment)) reasons |= EstimateReviewReason.MissingOwner;
        if (item.ReferralEndpoint == ReferralEndpoint.Unspecified) reasons |= EstimateReviewReason.MissingEndpoint;
        return reasons;
    }

    public static IEnumerable<EstimateReviewReason> IndividualReasons(EstimateReviewReason reasons) =>
        Enum.GetValues<EstimateReviewReason>().Where(reason => reason != EstimateReviewReason.None && reasons.HasFlag(reason));

    public static int Priority(EstimateReviewReason reasons) =>
        (reasons.HasFlag(EstimateReviewReason.MissingVerification) ? 8 : 0)
        + (reasons.HasFlag(EstimateReviewReason.Overdue) ? 4 : 0)
        + (reasons.HasFlag(EstimateReviewReason.MissingEndpoint) ? 2 : 0)
        + (reasons.HasFlag(EstimateReviewReason.MissingOwner) ? 1 : 0);
}
