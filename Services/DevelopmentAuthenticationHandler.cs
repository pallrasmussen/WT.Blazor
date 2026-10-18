using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AdGroupsOptions> adGroupsOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentLocal";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var groups = adGroupsOptions.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Environment.UserName),
            new(ClaimTypes.NameIdentifier, Environment.UserName),
            new(ClaimTypes.Role, groups.Viewer),
            new(ClaimTypes.Role, groups.Contributor),
            new(ClaimTypes.Role, groups.Admin),
            new(ClaimTypes.Role, "Administrators"),
            new(ClaimTypes.Role, @"BUILTIN\Administrators")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
