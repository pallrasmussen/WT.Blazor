using System.Globalization;

namespace WT.Blazor.Models;

public enum Hospital
{
    Sjukrahusverkid,
    Landssjukrahusid,
    Klaksvikar,
    Suduroyar
}

public enum Department
{
    Skurddepilin,
    Medisinskidepilin,
    Psykiatriskidepilin,
    Diagnostiskidepilin,
    Braddepilin
}

public enum WaitUnit
{
    Dagar,
    Vikur,
    Manadir
}

public enum HistoryAction
{
    Created,
    Updated,
    Edited,
    Reviewed,
    Deactivated,
    Reactivated
}

public static class DisplayNames
{
    private static bool English => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

    public static Hospital HospitalFrom(string displayName) =>
        TryHospitalFrom(displayName) ?? throw new InvalidOperationException($"Unknown hospital display name: {displayName}");

    public static Hospital? TryHospitalFrom(string? displayName) =>
        string.IsNullOrEmpty(displayName)
            ? null
            : Enum.GetValues<Hospital>()
                .Select(value => (Hospital?)value)
                .FirstOrDefault(value => value is not null && For(value.Value) == displayName);

    public static Department DepartmentFrom(string displayName) =>
        TryDepartmentFrom(displayName) ?? throw new InvalidOperationException($"Unknown department display name: {displayName}");

    public static Department? TryDepartmentFrom(string? displayName) =>
        string.IsNullOrEmpty(displayName)
            ? null
            : Enum.GetValues<Department>()
                .Select(value => (Department?)value)
                .FirstOrDefault(value => value is not null && For(value.Value) == displayName);

    public static string For(Hospital hospital) => hospital switch
    {
        Hospital.Sjukrahusverkid => English ? "Hospital Service" : "Sjúkrahúsverkið",
        Hospital.Landssjukrahusid => English ? "National Hospital" : "Landssjúkrahúsið",
        Hospital.Klaksvikar => English ? "Klaksvík Hospital" : "Klaksvíkar sjúkrahús",
        Hospital.Suduroyar => English ? "Suðuroy Hospital" : "Suðuroyar sjúkrahús",
        _ => hospital.ToString()
    };

    public static string For(Department department) => department switch
    {
        Department.Skurddepilin => English ? "Surgical Department" : "Skurðdepilin",
        Department.Medisinskidepilin => English ? "Medical Department" : "Medisinski depilin",
        Department.Psykiatriskidepilin => English ? "Psychiatric Department" : "Psykiatriski depilin",
        Department.Diagnostiskidepilin => English ? "Diagnostic Department" : "Diagnostiski depilin",
        Department.Braddepilin => English ? "Emergency Department" : "Bráðdepilin",
        _ => department.ToString()
    };

    // Faroese "mánaðir" becomes singular "mánaður" when the value is exactly one.
    public static string For(WaitUnit unit, int value) => unit switch
    {
        WaitUnit.Dagar => English ? (value == 1 ? "day" : "days") : "dagar",
        WaitUnit.Vikur => English ? (value == 1 ? "week" : "weeks") : "vikur",
        WaitUnit.Manadir => English ? (value == 1 ? "month" : "months") : (value == 1 ? "mánaður" : "mánaðir"),
        _ => unit.ToString()
    };

    public static string For(HistoryAction action) => action switch
    {
        HistoryAction.Created => English ? "Created" : "Stovnað",
        HistoryAction.Updated => English ? "Updated" : "Dagført",
        HistoryAction.Edited => English ? "Edited" : "Broytt",
        HistoryAction.Reviewed => English ? "Reviewed" : "Eftirkannað",
        HistoryAction.Deactivated => English ? "Removed from public view" : "Tikið av almennu síðuni",
        HistoryAction.Reactivated => English ? "Restored to public view" : "Lagt aftur á almennu síðuna",
        _ => action.ToString()
    };
}
