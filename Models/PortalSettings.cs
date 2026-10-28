namespace WT.Blazor.Models;

public sealed class PortalSettings
{
    public const int DefaultGreenMaximumAgeMonths = 3;
    public const int DefaultYellowMaximumAgeMonths = 6;
    public const int DefaultLongWaitMonths = 12;

    public int Id { get; set; } = 1;
    public int GreenMaximumAgeMonths { get; set; } = DefaultGreenMaximumAgeMonths;
    public int YellowMaximumAgeMonths { get; set; } = DefaultYellowMaximumAgeMonths;
    public int EstimateExpiryMonths { get; set; } = 12;
    public int LongWaitMonths { get; set; } = DefaultLongWaitMonths;
    public string? ReferralInstructionsFo { get; set; }
    public string? ReferralInstructionsEn { get; set; }
    public string? ContactGuidanceFo { get; set; }
    public string? ContactGuidanceEn { get; set; }
    public bool PublishPatientGuidance { get; set; }

    public int Version { get; set; } = 1;
}