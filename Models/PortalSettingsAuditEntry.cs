namespace WT.Blazor.Models;

public sealed class PortalSettingsAuditEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Username { get; set; } = string.Empty;
    public int OldGreenMaximumAgeMonths { get; set; }
    public int OldEstimateExpiryMonths { get; set; }
    public int NewEstimateExpiryMonths { get; set; }
    public int OldYellowMaximumAgeMonths { get; set; }
    public int OldLongWaitMonths { get; set; }
    public string? OldReferralInstructionsFo { get; set; }
    public string? OldReferralInstructionsEn { get; set; }
    public string? OldContactGuidanceFo { get; set; }
    public string? OldContactGuidanceEn { get; set; }
    public string? NewReferralInstructionsFo { get; set; }
    public string? NewReferralInstructionsEn { get; set; }
    public string? NewContactGuidanceFo { get; set; }
    public string? NewContactGuidanceEn { get; set; }
    public bool OldPublishPatientGuidance { get; set; }
    public bool NewPublishPatientGuidance { get; set; }
    public int NewGreenMaximumAgeMonths { get; set; }
    public int NewYellowMaximumAgeMonths { get; set; }
    public int NewLongWaitMonths { get; set; }
}