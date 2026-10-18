using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class HistoryExportRowTests
{
    [Fact]
    public void FromFormatsAuditEntryForAllExports()
    {
        var entry = new WaitingTimeHistoryEntry
        {
            Timestamp = new DateTime(2026, 9, 27, 12, 34, 0, DateTimeKind.Utc),
            Hospital = Hospital.Landssjukrahusid,
            Department = Department.Medisinskidepilin,
            Name = "Testviðgerð",
            Action = HistoryAction.Updated,
            Detail = "Bíðitíð broytt",
            OldWaitValue = 3,
            OldUnit = WaitUnit.Vikur,
            NewWaitValue = 2,
            NewUnit = WaitUnit.Manadir,
            Username = "heilsunet\\test-user",
            RequestId = "test-request-44"
        };

        var row = HistoryExportRow.From(entry);

        Assert.Equal(DisplayNames.For(Hospital.Landssjukrahusid), row.Hospital);
        Assert.Equal(DisplayNames.For(Department.Medisinskidepilin), row.Department);
        Assert.Equal($"3 {DisplayNames.For(WaitUnit.Vikur, 3)}", row.OldWait);
        Assert.Equal($"2 {DisplayNames.For(WaitUnit.Manadir, 2)}", row.NewWait);
        Assert.Equal("test-request-44", row.RequestId);
    }
}