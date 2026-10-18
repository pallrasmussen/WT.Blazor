using System.Security.Claims;
using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class PoliciesTests
{
    [Theory]
    [InlineData("DOMAIN\\wt-admins")]
    [InlineData("Administrators")]
    [InlineData("BUILTIN\\Administrators")]
    public void IsSuperAdminAcceptsConfiguredAndBuiltInAdministratorRoles(string role)
    {
        var user = UserWithRole(role);

        Assert.True(Policies.IsSuperAdmin(user, "DOMAIN\\wt-admins"));
    }

    [Fact]
    public void IsSuperAdminRejectsUnrelatedRoles()
    {
        Assert.False(Policies.IsSuperAdmin(UserWithRole("DOMAIN\\wt-viewers"), "DOMAIN\\wt-admins"));
    }

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
}