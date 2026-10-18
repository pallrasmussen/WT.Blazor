using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class UpdatedStatusTests
{
    [Fact]
    public void RequiresReviewUsesTheCalendarMonthCutoff()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(UpdatedStatus.RequiresReview(new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc), now));
        Assert.False(UpdatedStatus.RequiresReview(new DateTime(2026, 6, 27, 12, 0, 1, DateTimeKind.Utc), now));
    }

    [Fact]
    public void CssClassUsesTheSameCalendarMonthBoundaries()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("status-fresh", UpdatedStatus.CssClass(now.AddMonths(-3).AddTicks(1), now));
        Assert.Equal("status-warning", UpdatedStatus.CssClass(now.AddMonths(-3), now));
        Assert.Equal("status-old", UpdatedStatus.CssClass(now.AddMonths(-6), now));
    }

    [Fact]
    public void CssClassUsesConfiguredCalendarMonthBoundaries()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("status-fresh", UpdatedStatus.CssClass(now.AddMonths(-2).AddTicks(1), 2, 5, now));
        Assert.Equal("status-warning", UpdatedStatus.CssClass(now.AddMonths(-2), 2, 5, now));
        Assert.Equal("status-old", UpdatedStatus.CssClass(now.AddMonths(-5), 2, 5, now));
        Assert.True(UpdatedStatus.RequiresReview(now.AddMonths(-2), 2, now));
    }
}