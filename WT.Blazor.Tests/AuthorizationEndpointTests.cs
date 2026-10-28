using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WT.Blazor.Models;
using WT.Blazor.Tests.TestInfrastructure;
using Xunit;

namespace WT.Blazor.Tests;

[Collection(WebApplicationFactoryCollection.Name)]
public class AuthorizationEndpointTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"wt-blazor-auth-{Guid.NewGuid():N}.db");
    private readonly AuthenticatedWebApplicationFactory factory;
    private readonly HttpClient client;

    public AuthorizationEndpointTests()
    {
        factory = new AuthenticatedWebApplicationFactory(databasePath);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    [Fact]
    public async Task HealthIsPublicAndDeploymentDiagnosticsRequireAdminMembership()
    {
        var groups = factory.Services.GetRequiredService<IOptions<AdGroupsOptions>>().Value;

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/diagnostics/deployment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsAsync(groups.Viewer, "/diagnostics/deployment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsAsync(groups.Contributor, "/diagnostics/deployment")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsAsync(groups.Admin, "/diagnostics/deployment")).StatusCode);
    }

    [Theory]
    [InlineData(AuthenticatedWebApplicationFactory.ViewerGroup, false, false)]
    [InlineData(AuthenticatedWebApplicationFactory.ContributorGroup, true, false)]
    [InlineData(AuthenticatedWebApplicationFactory.AdminGroup, true, true)]
    public async Task AuthenticationDiagnosticsReportOnlyTheCallersEffectivePermissions(string role, bool edit, bool administer)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/diagnostics/authentication")).StatusCode);
        using var response = await GetAsAsync(role, "/diagnostics/authentication");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = document.RootElement;
        Assert.Equal("test-user", result.GetProperty("name").GetString());
        Assert.True(result.GetProperty("canView").GetBoolean());
        Assert.Equal(edit, result.GetProperty("canEdit").GetBoolean());
        Assert.Equal(administer, result.GetProperty("canAdminister").GetBoolean());
        Assert.False(result.GetProperty("isWindowsIdentity").GetBoolean());
        Assert.False(result.TryGetProperty("groups", out _));
    }

    [Fact]
    public async Task HistoryExportsRequireContributorOrAdminMembership()
    {
        var groups = factory.Services.GetRequiredService<IOptions<AdGroupsOptions>>().Value;

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/history/export/csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsAsync(groups.Viewer, "/api/history/export/csv")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsAsync(groups.Contributor, "/api/history/export/csv")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsAsync(groups.Admin, "/api/history/export/csv")).StatusCode);
    }

    [Fact]
    public async Task CultureEndpointRejectsUnknownCulturesAndSetsValidCultureCookie()
    {
        var invalid = await client.GetAsync("/culture/da?returnUrl=/");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var noRedirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var valid = await noRedirectClient.GetAsync("/culture/en?returnUrl=/gongd/12");
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        Assert.Equal("/gongd/12", valid.Headers.Location?.OriginalString);
        Assert.Contains(valid.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [], cookie => cookie.Contains(".AspNetCore.Culture", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("en", "Expected waiting time", "Hospital Service")]
    [InlineData("fo", "Væntað bíðitíð", "Sjúkrahúsverkið")]
    public async Task PublicDocumentUsesTheSelectedLanguageForHtmlTitleAndAccessibleNames(string culture, string waitingTime, string hospital)
    {
        await client.GetAsync($"/culture/{culture}?returnUrl=/");
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
        Assert.Contains($"<html lang=\"{culture}\">", html);
        Assert.Contains($"<title>{waitingTime} | {hospital}</title>", html);
        Assert.Contains($"aria-label=\"{waitingTime}\"", html);
        Assert.Contains($"aria-label=\"{hospital}\"", html);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
        if (Directory.Exists(factory.TemporaryPath)) Directory.Delete(factory.TemporaryPath, recursive: true);
    }

    private async Task<HttpResponseMessage> GetAsAsync(string role, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", role);
        return await client.SendAsync(request);
    }
}