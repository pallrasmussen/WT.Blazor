using WT.Blazor.Models;

namespace WT.Blazor.Services;

public record WaitingTimeComparison(string Name, ReferralEndpoint Endpoint, string Conditions, IReadOnlyList<WaitingTime> Estimates);

public static class WaitingTimeComparisons
{
    // Explicit procedure identity and exact stated conditions are conservative:
    // unspecified/ambiguous matches must never imply clinical interchangeability.
    public static IReadOnlyList<WaitingTimeComparison> Build(IEnumerable<WaitingTime> items) => items
        .Where(item => item.IsActive && item.EstimateStatus == EstimateStatus.Available
            && item.ReferralEndpoint != ReferralEndpoint.Unspecified && item.VerifiedAt is not null
            && item.Hospital != Hospital.Sjukrahusverkid && !string.IsNullOrWhiteSpace(item.ComparisonCode) && !string.IsNullOrWhiteSpace(item.Note))
        .GroupBy(item => (Code: item.ComparisonCode!.Trim().ToUpperInvariant(), item.ReferralEndpoint, Conditions: item.Note!.Trim()))
        .Where(group => group.Select(item => item.Hospital).Distinct().Count() >= 2
            && group.Select(item => item.Hospital).Distinct().Count() == group.Count())
        .Select(group => new WaitingTimeComparison(group.OrderBy(item => item.Id).First().Name, group.Key.ReferralEndpoint,
            group.Key.Conditions, group.OrderBy(item => item.Hospital).ToList()))
        .OrderBy(group => group.Name).ToList();
}
