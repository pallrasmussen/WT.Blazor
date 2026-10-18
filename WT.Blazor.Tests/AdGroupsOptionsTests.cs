using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class AdGroupsOptionsTests
{
    [Fact]
    public void ApplyDefaultsFillsMissingGroupsWithoutReplacingConfiguredValues()
    {
        var options = new AdGroupsOptions { Admin = "DOMAIN\\wt-admins" };

        options.ApplyDefaults("SERVER01");

        Assert.Equal(@"SERVER01\WT-Viewers", options.Viewer);
        Assert.Equal(@"SERVER01\WT-Contributors", options.Contributor);
        Assert.Equal("DOMAIN\\wt-admins", options.Admin);
    }

    [Fact]
    public void ApplyDefaultsUsesTheLegacyContributorAlias()
    {
        var options = new AdGroupsOptions { Contributer = "DOMAIN\\legacy-contributors" };

        options.ApplyDefaults("SERVER01");

        Assert.Equal("DOMAIN\\legacy-contributors", options.Contributor);
    }

    [Fact]
    public void ValidatorRejectsDuplicateGroups()
    {
        var result = new AdGroupsOptionsValidator().Validate(null, new AdGroupsOptions
        {
            Viewer = "DOMAIN\\same",
            Contributor = "DOMAIN\\same",
            Admin = "DOMAIN\\admins"
        });

        Assert.True(result.Failed);
    }
}