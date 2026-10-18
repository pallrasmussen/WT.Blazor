using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
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

namespace WT.Blazor.Tests;

internal sealed class ExportWebApplicationFactory(string databasePath, string temporaryPath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", $"Data Source={databasePath}"),
            new KeyValuePair<string, string?>("Exports:MaximumTrendPoints", "1"),
            new KeyValuePair<string, string?>("Exports:TemporaryPath", temporaryPath)
        ]));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={databasePath};Pooling=False"));
            services.PostConfigure<ExportOptions>(options =>
            {
                options.MaximumTrendPoints = 1;
                options.TemporaryPath = temporaryPath;
            });
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddSingleton<IAuthenticationSchemeProvider, TestAuthenticationSchemeProvider>();
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }

    private sealed class TestAuthenticationSchemeProvider(IOptions<AuthenticationOptions> options)
        : AuthenticationSchemeProvider(options)
    {
        public override Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() =>
            Task.FromResult<IEnumerable<AuthenticationScheme>>([]);
    }
}