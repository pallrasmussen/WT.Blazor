using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HistoryQueryMapperTests
{
    [Fact]
    public void FromExportParametersMapsFiltersAndMakesTheEndDateExclusive()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 27);

        var query = HistoryQueryMapper.FromExportParameters(
            DisplayNames.For(Hospital.Landssjukrahusid),
            DisplayNames.For(Department.Medisinskidepilin),
            "MR-skanning",
            nameof(HistoryAction.Updated),
            "heilsunet\\test-user",
            "broyting",
            "name-asc",
            from,
            to);

        Assert.Equal(Hospital.Landssjukrahusid, query.Hospital);
        Assert.Equal(Department.Medisinskidepilin, query.Department);
        Assert.Equal(HistoryAction.Updated, query.Action);
        Assert.Equal(from, query.FromUtc);
        Assert.Equal(to.AddDays(1), query.ToUtc);
        Assert.Equal("name", query.SortBy);
        Assert.False(query.Descending);
    }

    [Fact]
    public void FromParametersMapsPagingForTheHistoryPage()
    {
        var query = HistoryQueryMapper.FromParameters(
            null,
            null,
            null,
            null,
            null,
            null,
            "timestamp-desc",
            null,
            null,
            3,
            100);

        Assert.Equal(3, query.PageNumber);
        Assert.Equal(100, query.PageSize);
        Assert.Equal("timestamp", query.SortBy);
        Assert.True(query.Descending);
    }

    [Fact]
    public void FromValuesPreservesTypedFilters()
    {
        var query = HistoryQueryMapper.FromValues(
            Hospital.Landssjukrahusid,
            Department.Medisinskidepilin,
            "MR-skanning",
            HistoryAction.Updated,
            "heilsunet\\test-user",
            "broyting",
            "action-asc",
            null,
            null);

        Assert.Equal(Hospital.Landssjukrahusid, query.Hospital);
        Assert.Equal(Department.Medisinskidepilin, query.Department);
        Assert.Equal(HistoryAction.Updated, query.Action);
        Assert.Equal("action", query.SortBy);
        Assert.False(query.Descending);
    }

    [Fact]
    public void FromExportParametersIgnoresUnknownDisplayLabels()
    {
        var query = HistoryQueryMapper.FromExportParameters(
            "Unknown hospital",
            "Unknown department",
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Null(query.Hospital);
        Assert.Null(query.Department);
    }

    [Fact]
    public void ExportRequestConvertsBoundParametersToAHistoryQuery()
    {
        var query = new HistoryExportRequest
        {
            Name = "MR-skanning",
            Action = nameof(HistoryAction.Created),
            Q = "skanning",
            Sort = "action-asc"
        }.ToHistoryQuery();

        Assert.Equal("MR-skanning", query.Name);
        Assert.Equal(HistoryAction.Created, query.Action);
        Assert.Equal("skanning", query.Search);
        Assert.Equal("action", query.SortBy);
        Assert.False(query.Descending);
    }
}