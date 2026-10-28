using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using WT.Blazor.Models;

namespace WT.Blazor.Services;

public static class AuthenticationDiagnosticsEndpoint
{
    public static RouteHandlerBuilder MapAuthenticationDiagnostics(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/diagnostics/authentication", async (ClaimsPrincipal user, IAuthorizationService authorization, HttpResponse response) =>
        {
            // Return only the caller's effective permissions; never enumerate AD groups.
            response.Headers.CacheControl = "no-store";
            return Results.Ok(new
            {
                name = user.Identity?.Name,
                authenticationType = user.Identity?.AuthenticationType,
                isWindowsIdentity = OperatingSystem.IsWindows() && user.Identity is WindowsIdentity,
                isIisHosted = WindowsAuthenticationConfiguration.IsIisHosted(Environment.GetEnvironmentVariable(WindowsAuthenticationConfiguration.IisPhysicalPathEnvironmentVariable)),
                canView = (await authorization.AuthorizeAsync(user, Policies.ViewerAccess)).Succeeded,
                canEdit = (await authorization.AuthorizeAsync(user, Policies.AdminAccess)).Succeeded,
                canAdminister = (await authorization.AuthorizeAsync(user, Policies.SuperAdminAccess)).Succeeded
            });
        }).RequireAuthorization(Policies.ViewerAccess);
}
