namespace WT.Blazor.Models;

public static class WaitDuration
{
    public const int LongWaitDays = 365;

    public static int ToDays(int value, WaitUnit unit) => unit switch
    {
        WaitUnit.Dagar => value,
        WaitUnit.Vikur => value * 7,
        WaitUnit.Manadir => value * 30,
        _ => value
    };

    public static int FromDays(int days, WaitUnit unit) => unit switch
    {
        WaitUnit.Dagar => days,
        WaitUnit.Vikur => (int)Math.Round(days / 7d, MidpointRounding.AwayFromZero),
        WaitUnit.Manadir => (int)Math.Round(days / 30d, MidpointRounding.AwayFromZero),
        _ => days
    };

    public static int MaximumValueAtOrBelowDays(int days, WaitUnit unit) =>
        days / ToDays(1, unit);
}