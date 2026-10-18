namespace WT.Blazor.Models;

public sealed class PortalSettingsAuditEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Username { get; set; } = string.Empty;
    public int OldGreenMaximumAgeMonths { get; set; }
    public int OldYellowMaximumAgeMonths { get; set; }
    public int OldLongWaitMonths { get; set; }
    public int NewGreenMaximumAgeMonths { get; set; }
    public int NewYellowMaximumAgeMonths { get; set; }
    public int NewLongWaitMonths { get; set; }
}