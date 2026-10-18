using WT.Blazor.Models;

namespace WT.Blazor.Services;

public static class HistoryQueryMapper
{
    public static HistoryQuery FromParameters(
        string? hospital,
        string? department,
        string? name,
        string? action,
        string? username,
        string? search,
        string? sort,
        DateTime? from,
        DateTime? to,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var hospitalValue = DisplayNames.TryHospitalFrom(hospital);
        var departmentValue = DisplayNames.TryDepartmentFrom(department);
        HistoryAction? actionValue = Enum.TryParse<HistoryAction>(action, out var parsedAction) ? parsedAction : null;
        return FromValues(hospitalValue, departmentValue, name, actionValue, username, search, sort, from, to, pageNumber, pageSize);
    }

    public static HistoryQuery FromValues(
        Hospital? hospital,
        Department? department,
        string? name,
        HistoryAction? action,
        string? username,
        string? search,
        string? sort,
        DateTime? from,
        DateTime? to,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var sortParts = sort?.Split('-', 2) ?? [];
        return new HistoryQuery(hospital, department, name, action, username, from, to?.AddDays(1), search, sortParts.ElementAtOrDefault(0), sortParts.ElementAtOrDefault(1) != "asc", pageNumber, pageSize);
    }

    public static HistoryQuery FromExportParameters(
        string? hospital,
        string? department,
        string? name,
        string? action,
        string? username,
        string? search,
        string? sort,
        DateTime? from,
        DateTime? to) =>
        FromParameters(hospital, department, name, action, username, search, sort, from, to);
}