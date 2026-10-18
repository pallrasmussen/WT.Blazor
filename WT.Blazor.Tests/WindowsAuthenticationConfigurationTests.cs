using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class WindowsAuthenticationConfigurationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("C:\\inetpub\\wwwroot\\WT.Blazor", true)]
    public void IsIisHostedUsesTheIisPhysicalPathMarker(string? iisPhysicalPath, bool expected) =>
        Assert.Equal(expected, WindowsAuthenticationConfiguration.IsIisHosted(iisPhysicalPath));

    [Fact]
    public void ConfigureUsesTheDevelopmentFallbackInLocalDevelopment()
    {
        var services = new ServiceCollection();
        services.AddOptions<AdGroupsOptions>()
            .Configure(options =>
            {
                options.Viewer = @"TEST-SERVER\WT-Viewers";
                options.Contributor = @"TEST-SERVER\WT-Contributors";
                options.Admin = @"TEST-SERVER\WT-Admins";
            });

        WindowsAuthenticationConfiguration.Configure(services, null, isDevelopment: true);

        var provider = services.BuildServiceProvider();
        var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(DevelopmentAuthenticationHandler.SchemeName, authOptions.DefaultScheme);
        Assert.Equal(DevelopmentAuthenticationHandler.SchemeName, authOptions.DefaultAuthenticateScheme);
        Assert.Equal(DevelopmentAuthenticationHandler.SchemeName, authOptions.DefaultChallengeScheme);
    }
}