using System.ComponentModel.DataAnnotations;

namespace WT.Blazor.Models;

public class WaitingTime
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public Hospital Hospital { get; set; }

    // Only meaningful when Hospital is Landssjukrahusid.
    public Department? Department { get; set; }

    [Range(0, int.MaxValue)]
    public int WaitValue { get; set; }

    public WaitUnit Unit { get; set; }

    public EstimateStatus EstimateStatus { get; set; }
    public ReferralEndpoint ReferralEndpoint { get; set; }

    [StringLength(200)]
    public string? ResponsibleDepartment { get; set; }

    [StringLength(100)]
    public string? ComparisonCode { get; set; }

    // Start a new comparable trend when its definition changes or an estimate resumes.
    public int EstimateSeries { get; set; } = 1;

    [StringLength(500)]
    public string? Note { get; set; }

    // Last estimate/status/definition change or explicit review; edit times live in History.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Clinical estimate confirmation, independent of metadata edits and availability reviews.
    public DateTime? VerifiedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; } = 1;

    public List<WaitingTimeHistoryEntry> History { get; set; } = [];
}
