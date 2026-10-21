namespace WT.Blazor.Models;

public static class WaitDuration
{
    public const int LongWaitDays = 365;

    public static int MaximumValue(WaitUnit unit) => int.MaxValue / DaysPerUnit(unit);

    public static int ToDays(int value, WaitUnit unit)
    {
        var factor = DaysPerUnit(unit);
        if (value < 0 || value > int.MaxValue / factor)
            throw new ArgumentOutOfRangeException(nameof(value), "The duration cannot be represented as non-negative canonical days.");
        return checked(value * factor);
    }

    public static int FromDays(int days, WaitUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        return (int)Math.Round(days / (double)DaysPerUnit(unit), MidpointRounding.AwayFromZero);
    }

    public static int MaximumValueAtOrBelowDays(int days, WaitUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        return days / DaysPerUnit(unit);
    }

    private static int DaysPerUnit(WaitUnit unit) => unit switch
    {
        WaitUnit.Dagar => 1,
        WaitUnit.Vikur => 7,
        WaitUnit.Manadir => 30,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
}
