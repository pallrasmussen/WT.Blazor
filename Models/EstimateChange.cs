namespace WT.Blazor.Models;

public static class EstimateChange
{
    // Compare canonical days, so changing display units alone never triggers a warning.
    public static bool RequiresConfirmation(WaitingTime current, EstimateStatus status, ReferralEndpoint endpoint, int value, WaitUnit unit, int? upperValue = null)
    {
        if (current.EstimateStatus != EstimateStatus.Available || status != EstimateStatus.Available || current.ReferralEndpoint != endpoint) return false;
        if (value < 0 || value > WaitDuration.MaximumValue(unit)) return false;
        if (upperValue is { } upper && (upper < value || upper > WaitDuration.MaximumValue(unit))) return false;
        var upperChange = RequiresLargeChange(current.WaitUpperValue ?? current.WaitValue, current.Unit, upperValue ?? value, unit);
        return upperChange || RequiresLargeChange(current.WaitValue, current.Unit, value, unit);
    }

    private static bool RequiresLargeChange(int previousValue, WaitUnit previousUnit, int nextValue, WaitUnit nextUnit)
    {
        var previous = (long)WaitDuration.ToDays(previousValue, previousUnit);
        var next = (long)WaitDuration.ToDays(nextValue, nextUnit);
        return Math.Abs(next - previous) >= 28 && (next >= previous * 2 || previous >= next * 2);
    }
}
