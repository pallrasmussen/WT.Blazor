using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WT.Blazor.Data;
using WT.Blazor.Models;

namespace WT.Blazor.Tests.TestInfrastructure;

internal sealed class AuthenticatedWebApplicationFactory(string databasePath) : WebApplicationFactory<Program>
{
    public const string ViewerGroup = @"TEST-SERVER\WT-Viewers";
    public const string ContributorGroup = @"TEST-SERVER\WT-Contributors";
    public const string AdminGroup = @"TEST-SERVER\WT-Admins";
    public string TemporaryPath { get; } = Path.Combine(Path.GetTempPath(), $"wt-blazor-auth-exports-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        Directory.CreateDirectory(TemporaryPath);
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", $"Data Source={databasePath}"),
            new KeyValuePair<string, string?>("AdGroups:Viewer", ViewerGroup),
            new KeyValuePair<string, string?>("AdGroups:Contributor", ContributorGroup),
            new KeyValuePair<string, string?>("AdGroups:Admin", AdminGroup),
            new KeyValuePair<string, string?>("Exports:TemporaryPath", TemporaryPath)
        ]));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={databasePath};Pooling=False"));
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddSingleton<IAuthenticationSchemeProvider, TestAuthenticationSchemeProvider>();
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });
            services.PostConfigure<AuthorizationOptions>(options =>
            {
                options.AddPolicy(Policies.ViewerAccess, policy => policy.RequireRole(ViewerGroup, ContributorGroup, AdminGroup));
                options.AddPolicy(Policies.AdminAccess, policy => policy.RequireRole(ContributorGroup, AdminGroup));
                options.AddPolicy(Policies.SuperAdminAccess, policy => policy.RequireAssertion(context => Policies.IsSuperAdmin(context.User, AdminGroup)));
            });
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "test-user"),
                new(ClaimTypes.Role, role)
            };
            if (role.EndsWith(@"\WT-Admins", StringComparison.OrdinalIgnoreCase))
                claims.Add(new Claim(ClaimTypes.Role, @"BUILTIN\Administrators"));

            var identity = new ClaimsIdentity(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class TestAuthenticationSchemeProvider(IOptions<AuthenticationOptions> options)
        : AuthenticationSchemeProvider(options)
    {
        public override Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() =>
            Task.FromResult<IEnumerable<AuthenticationScheme>>([]);
    }
}