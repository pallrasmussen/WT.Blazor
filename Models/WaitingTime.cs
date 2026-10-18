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

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public int Version { get; set; } = 1;

    public List<WaitingTimeHistoryEntry> History { get; set; } = [];
}
