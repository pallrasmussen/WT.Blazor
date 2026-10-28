using WT.Blazor.Models;

namespace WT.Blazor.Components.Pages;

public sealed class WaitingTimeFormState
{
    public string HospitalName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string Name { get; set; } = "";
    public int WaitValue { get; set; }
    public WaitUnit Unit { get; set; } = WaitUnit.Manadir;
    public EstimateStatus EstimateStatus { get; set; } = EstimateStatus.Unknown;
    public ReferralEndpoint ReferralEndpoint { get; set; }
    public string? ResponsibleDepartment { get; set; }
    public string? ComparisonCode { get; set; }
    public string? Note { get; set; }

    public void Load(WaitingTime item)
    {
        HospitalName = DisplayNames.For(item.Hospital);
        DepartmentName = item.Department is null ? "" : DisplayNames.For(item.Department.Value);
        Name = item.Name;
        WaitValue = item.WaitValue;
        Unit = item.Unit;
        Note = item.Note;
        EstimateStatus = item.EstimateStatus;
        ReferralEndpoint = item.ReferralEndpoint;
        ResponsibleDepartment = item.ResponsibleDepartment;
        ComparisonCode = item.ComparisonCode;
    }

    public void Clear()
    {
        HospitalName = "";
        DepartmentName = "";
        Name = "";
        WaitValue = 0;
        Unit = WaitUnit.Manadir;
        Note = null;
        EstimateStatus = EstimateStatus.Unknown;
        ReferralEndpoint = ReferralEndpoint.Unspecified;
        ResponsibleDepartment = null;
        ComparisonCode = null;
    }
}