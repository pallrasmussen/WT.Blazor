using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class DisplayNamesTests
{
    [Fact]
    public void HospitalLabelsRoundTripToTheirEnumValues()
    {
        foreach (var hospital in Enum.GetValues<Hospital>())
        {
            Assert.Equal(hospital, DisplayNames.HospitalFrom(DisplayNames.For(hospital)));
        }
    }

    [Fact]
    public void DepartmentLabelsRoundTripToTheirEnumValues()
    {
        foreach (var department in Enum.GetValues<Department>())
        {
            Assert.Equal(department, DisplayNames.DepartmentFrom(DisplayNames.For(department)));
        }
    }

    [Fact]
    public void TryMethodsReturnNullForUnknownDisplayLabels()
    {
        Assert.Null(DisplayNames.TryHospitalFrom("Unknown hospital"));
        Assert.Null(DisplayNames.TryDepartmentFrom("Unknown department"));
    }
}