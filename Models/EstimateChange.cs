namespace WT.Blazor.Models;

public static class EstimateChange
{
    // Compare canonical days, so changing display units alone never triggers a warning.
    public static bool RequiresConfirmation(WaitingTime current, EstimateStatus status, ReferralEndpoint endpoint, int value, WaitUnit unit)
    {
        if (current.EstimateStatus != EstimateStatus.Available || status != EstimateStatus.Available || current.ReferralEndpoint != endpoint) return false;
        if (value < 0 || value > WaitDuration.MaximumValue(unit)) return false;
        var previous = (long)WaitDuration.ToDays(current.WaitValue, current.Unit);
        var next = (long)WaitDuration.ToDays(value, unit);
        return Math.Abs(next - previous) >= 28 && (next >= previous * 2 || previous >= next * 2);
    }
}
