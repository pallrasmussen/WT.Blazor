using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class WaitDurationTests
{
    [Theory]
    [InlineData(WaitUnit.Dagar, int.MaxValue, int.MaxValue)]
    [InlineData(WaitUnit.Vikur, 306783378, 2147483646)]
    [InlineData(WaitUnit.Manadir, 71582788, 2147483640)]
    public void LargestSupportedDurationConvertsWithoutOverflow(WaitUnit unit, int value, int days)
    {
        Assert.Equal(value, WaitDuration.MaximumValue(unit));
        Assert.Equal(days, WaitDuration.ToDays(value, unit));
        Assert.Equal(0, WaitDuration.ToDays(0, unit));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaitDuration.ToDays(-1, unit));
        if (value < int.MaxValue)
            Assert.Throws<ArgumentOutOfRangeException>(() => WaitDuration.ToDays(value + 1, unit));
    }

    [Fact]
    public void InvalidUnitsAndNegativeCanonicalDaysAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WaitDuration.ToDays(1, (WaitUnit)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaitDuration.FromDays(-1, WaitUnit.Dagar));
        Assert.Throws<ArgumentOutOfRangeException>(() => WaitDuration.MaximumValueAtOrBelowDays(-1, WaitUnit.Dagar));
    }

    [Theory]
    [InlineData(WaitUnit.Dagar, 365)]
    [InlineData(WaitUnit.Vikur, 52)]
    [InlineData(WaitUnit.Manadir, 12)]
    public void MaximumValueAtOrBelowDaysUsesCanonicalLongWaitThreshold(WaitUnit unit, int expectedValue)
    {
        Assert.Equal(expectedValue, WaitDuration.MaximumValueAtOrBelowDays(WaitDuration.LongWaitDays, unit));
    }
}
