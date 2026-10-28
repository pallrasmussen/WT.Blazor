using WT.Blazor.Localization;
using WT.Blazor.Models;

namespace WT.Blazor.Components.Administration;

internal static class WaitingTimeEditPreview
{
    public static IEnumerable<string> Changes(WaitingTimeInput previous, WaitingTimeInput current)
    {
        if (previous.EstimateStatus != current.EstimateStatus)
            yield return Change("EstimateStatus", ReferralEstimateText.StatusLabel(previous.EstimateStatus), ReferralEstimateText.StatusLabel(current.EstimateStatus));
        if (previous.ReferralEndpoint != current.ReferralEndpoint)
            yield return Change("ReferralEndpoint", ReferralEstimateText.EndpointLabel(previous.ReferralEndpoint), ReferralEstimateText.EndpointLabel(current.ReferralEndpoint));
        if (previous.WaitValue != current.WaitValue || previous.WaitUpperValue != current.WaitUpperValue || previous.Unit != current.Unit)
            yield return Change("Wait", Duration(previous), Duration(current));
        if (previous.Name != current.Name) yield return Change("SpecialtyTreatment", previous.Name, current.Name);
        if (previous.Hospital != current.Hospital) yield return Change("Hospital", HospitalName(previous.Hospital), HospitalName(current.Hospital));
        if (previous.Department != current.Department) yield return Change("Department", DepartmentName(previous.Department), DepartmentName(current.Department));
        if (previous.ResponsibleDepartment != current.ResponsibleDepartment) yield return Change("ResponsibleDepartment", previous.ResponsibleDepartment, current.ResponsibleDepartment);
        if (previous.ComparisonCode != current.ComparisonCode) yield return Change("ComparisonCode", previous.ComparisonCode, current.ComparisonCode);
        if ((previous.Note ?? "") != (current.Note ?? "")) yield return Change("Note", previous.Note, current.Note);
    }

    private static string Change(string label, string? previous, string? current) => $"{AppText.Get(label)}: {previous ?? "–"} → {current ?? "–"}";
    private static string HospitalName(Hospital? hospital) => hospital is { } value ? DisplayNames.For(value) : AppText.Get("ChooseHospital");
    private static string DepartmentName(Department? department) => department is { } value ? DisplayNames.For(value) : "–";
    private static string Duration(WaitingTimeInput input) => input.EstimateStatus != EstimateStatus.Available
        ? ReferralEstimateText.StatusLabel(input.EstimateStatus)
        : $"{(input.WaitUpperValue is { } upper ? $"{input.WaitValue}–{upper}" : input.WaitValue.ToString())} {DisplayNames.For(input.Unit, input.WaitUpperValue ?? input.WaitValue)}";
}
