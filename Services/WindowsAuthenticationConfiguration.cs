using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.DependencyInjection;

namespace WT.Blazor.Services;

public static class WindowsAuthenticationConfiguration
{
    public const string IisPhysicalPathEnvironmentVariable = "ASPNETCORE_IIS_PHYSICAL_PATH";

    public static bool IsIisHosted(string? iisPhysicalPath) => !string.IsNullOrWhiteSpace(iisPhysicalPath);

    public static void Configure(IServiceCollection services, string? iisPhysicalPath, bool isDevelopment = false)
    {
        if (IsIisHosted(iisPhysicalPath))
        {
            services.AddAuthentication(IISDefaults.AuthenticationScheme);
            return;
        }

        if (isDevelopment)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = DevelopmentAuthenticationHandler.SchemeName;
                options.DefaultAuthenticateScheme = DevelopmentAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = DevelopmentAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, _ => { });
            return;
        }

        services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate();
    }
}