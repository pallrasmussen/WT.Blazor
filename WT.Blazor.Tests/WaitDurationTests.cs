using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class WaitDurationTests
{
    [Theory]
    [InlineData(WaitUnit.Dagar, 365)]
    [InlineData(WaitUnit.Vikur, 52)]
    [InlineData(WaitUnit.Manadir, 12)]
    public void MaximumValueAtOrBelowDaysUsesCanonicalLongWaitThreshold(WaitUnit unit, int expectedValue)
    {
        Assert.Equal(expectedValue, WaitDuration.MaximumValueAtOrBelowDays(WaitDuration.LongWaitDays, unit));
    }
}