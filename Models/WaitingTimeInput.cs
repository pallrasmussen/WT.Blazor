namespace WT.Blazor.Models;

// Editable data only: identity, concurrency and audit context belong to commands.
public record WaitingTimeInput
{
    public string Name { get; init; } = "";
    public Hospital? Hospital { get; init; }
    public Department? Department { get; init; }
    public int WaitValue { get; init; }
    public int? WaitUpperValue { get; init; }
    public WaitUnit Unit { get; init; }
    public string? Note { get; init; }
    public EstimateStatus EstimateStatus { get; init; } = EstimateStatus.Available;
    public ReferralEndpoint ReferralEndpoint { get; init; }
    public string? ResponsibleDepartment { get; init; }
    public string? ComparisonCode { get; init; }

    public static WaitingTimeInput From(WaitingTime item) => new()
    {
        Name = item.Name,
        Hospital = item.Hospital,
        Department = item.Department,
        WaitValue = item.WaitValue,
        WaitUpperValue = item.WaitUpperValue,
        Unit = item.Unit,
        Note = item.Note,
        EstimateStatus = item.EstimateStatus,
        ReferralEndpoint = item.ReferralEndpoint,
        ResponsibleDepartment = item.ResponsibleDepartment,
        ComparisonCode = item.ComparisonCode
    };
}
