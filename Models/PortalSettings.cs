namespace WT.Blazor.Models;

public sealed class PortalSettings
{
    public const int DefaultGreenMaximumAgeMonths = 3;
    public const int DefaultYellowMaximumAgeMonths = 6;
    public const int DefaultLongWaitMonths = 12;

    public int Id { get; set; } = 1;
    public int GreenMaximumAgeMonths { get; set; } = DefaultGreenMaximumAgeMonths;
    public int YellowMaximumAgeMonths { get; set; } = DefaultYellowMaximumAgeMonths;
    public int LongWaitMonths { get; set; } = DefaultLongWaitMonths;
    public int Version { get; set; } = 1;
}