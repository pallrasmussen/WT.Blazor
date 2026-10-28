using WT.Blazor.Models;

namespace WT.Blazor.Components.Administration;

public sealed class AdministrationFilterState
{
    public Hospital? Hospital { get; set; }
    public Department? Department { get; set; }
    public string Search { get; set; } = "";
    public bool NeedsReview { get; set; }
    public bool Inactive { get; set; }
    public EstimateReviewReason Reason { get; set; }
    public string Owner { get; set; } = "";
}
