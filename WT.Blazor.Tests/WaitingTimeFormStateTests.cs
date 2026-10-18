using WT.Blazor.Components.Pages;
using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class WaitingTimeFormStateTests
{
    [Fact]
    public void LoadCopiesEveryEditableWaitingTimeField()
    {
        var item = new WaitingTime
        {
            Name = "Hjartaskurðviðgerð",
            Hospital = Hospital.Landssjukrahusid,
            Department = Department.Medisinskidepilin,
            WaitValue = 12,
            Unit = WaitUnit.Vikur,
            Note = "Fastandi tilvísing"
        };
        var state = new WaitingTimeFormState();

        state.Load(item);

        Assert.Equal(DisplayNames.For(item.Hospital), state.HospitalName);
        Assert.Equal(DisplayNames.For(item.Department.Value), state.DepartmentName);
        Assert.Equal(item.Name, state.Name);
        Assert.Equal(item.WaitValue, state.WaitValue);
        Assert.Equal(item.Unit, state.Unit);
        Assert.Equal(item.Note, state.Note);
    }

    [Fact]
    public void ClearRestoresCreateFormDefaults()
    {
        var state = new WaitingTimeFormState
        {
            HospitalName = "Klaksvíkar Sjúkrahús",
            DepartmentName = "Any department",
            Name = "Any name",
            WaitValue = 9,
            Unit = WaitUnit.Vikur,
            Note = "Any note"
        };

        state.Clear();

        Assert.Equal("", state.HospitalName);
        Assert.Equal("", state.DepartmentName);
        Assert.Equal("", state.Name);
        Assert.Equal(0, state.WaitValue);
        Assert.Equal(WaitUnit.Manadir, state.Unit);
        Assert.Null(state.Note);
    }
}