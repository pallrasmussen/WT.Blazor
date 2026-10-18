using System.Security.Claims;

namespace WT.Blazor.Models;

// Authorization policy names. Membership is resolved from Windows security groups
// configured under "AdGroups:Viewer", "AdGroups:Contributor", and "AdGroups:Admin".
public static class Policies
{
    public const string ViewerAccess = "ViewerAccess";
    public const string AdminAccess = "AdminAccess";
    public const string SuperAdminAccess = "SuperAdminAccess";

    public static bool IsSuperAdmin(ClaimsPrincipal user, string adminGroup) =>
        user.IsInRole(adminGroup)
        || user.IsInRole("Administrators")
        || user.IsInRole(@"BUILTIN\Administrators");
}
