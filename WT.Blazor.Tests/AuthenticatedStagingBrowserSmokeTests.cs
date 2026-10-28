using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace WT.Blazor.Tests;

public class AuthenticatedStagingBrowserSmokeTests : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private readonly string baseUrl = Environment.GetEnvironmentVariable("BROWSER_BASE_URL")?.TrimEnd('/') ?? "";

    [StagingRoleBrowserFact("Viewer")]
    public async Task ViewerCannotUseInternalAdministrationControls()
    {
        var page = await NewPageAsync();
        await AssertWindowsIdentityAsync(page);
        var response = await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.NotNull(response);
        Assert.Equal(403, response.Status);
        // Missing controls alone would also pass for a failed sign-in or error page.
        await Assertions.Expect(page.Locator(".access-message")).ToBeVisibleAsync();

        Assert.Equal(0, await page.Locator("#show-inactive").CountAsync());
        Assert.Equal(0, await page.Locator(".admin-actions").CountAsync());
        await AssertStatusAsync(page, "/api/history/export/csv", 403);
        await AssertStatusAsync(page, "/internal/settings", 403);
        await AssertStatusAsync(page, "/diagnostics/deployment", 403);
    }

    [StagingRoleBrowserFact("Contributor")]
    public async Task ContributorCanEditButCannotUseAdminOnlyControls()
    {
        var page = await NewPageAsync();
        await AssertWindowsIdentityAsync(page);
        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator(".admin-actions .btn-teal").First).ToBeVisibleAsync();
        Assert.Equal(0, await page.Locator("#show-inactive").CountAsync());
        Assert.Equal(0, await page.Locator("button.btn-deactivate").CountAsync());
        await AssertStatusAsync(page, "/internal/settings", 403);
        await AssertStatusAsync(page, "/diagnostics/deployment", 403);
        await AssertStatusAsync(page, "/api/history/export/csv", 200);
    }

    [StagingRoleBrowserFact("Admin")]
    public async Task AdminSeesLifecycleControls()
    {
        var page = await NewPageAsync();
        await AssertWindowsIdentityAsync(page);
        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator("#show-inactive")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button.btn-deactivate").First).ToBeVisibleAsync();
        await AssertStatusAsync(page, "/internal/settings", 200);
        await Assertions.Expect(page.Locator("#expiry-months")).ToBeVisibleAsync();
        await AssertStatusAsync(page, "/diagnostics/deployment", 200);
        await AssertStatusAsync(page, "/api/history/export/csv", 200);
    }

    public async Task InitializeAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Authenticated staging checks require Windows.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var stagingUri) || stagingUri.Scheme != "https" || !string.IsNullOrEmpty(stagingUri.UserInfo))
            throw new InvalidOperationException("Supply an HTTPS staging URL without embedded credentials.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STAGING_BROWSER_EXPECTED_IDENTITY")))
            throw new InvalidOperationException("Supply STAGING_BROWSER_EXPECTED_IDENTITY=DOMAIN\\account.");
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            // Chromium must be allowed to negotiate as the runner's Windows identity.
            Args = [$"--auth-server-allowlist={new Uri(baseUrl).Host}"]
        });
    }

    public async Task DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }

    private async Task AssertWindowsIdentityAsync(IPage page)
    {
        var response = await page.GotoAsync($"{baseUrl}/diagnostics/authentication");
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        using var document = JsonDocument.Parse(await response.TextAsync());
        var result = document.RootElement;
        var expectedIdentity = Environment.GetEnvironmentVariable("STAGING_BROWSER_EXPECTED_IDENTITY");
        Assert.False(string.IsNullOrWhiteSpace(expectedIdentity), "Supply the dedicated DOMAIN\\account expected for this runner.");
        Assert.True(string.Equals(expectedIdentity, result.GetProperty("name").GetString(), StringComparison.OrdinalIgnoreCase), "The site authenticated a different Windows account.");
        Assert.True(result.GetProperty("isWindowsIdentity").GetBoolean(), "The site must authenticate a real Windows identity.");
        Assert.True(result.GetProperty("isIisHosted").GetBoolean(), "The role check must run against IIS.");
        Assert.True(result.GetProperty("canView").GetBoolean());
        var role = Environment.GetEnvironmentVariable("STAGING_BROWSER_ROLE");
        Assert.Equal(string.Equals(role, "Contributor", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase), result.GetProperty("canEdit").GetBoolean());
        Assert.Equal(string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase), result.GetProperty("canAdminister").GetBoolean());
    }

    private async Task AssertStatusAsync(IPage page, string path, int expectedStatus)
    {
        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            var status = await page.EvaluateAsync<int>("async path => { const response = await fetch(path, { credentials: 'same-origin' }); await response.body?.cancel(); return response.status; }", path);
            Assert.Equal(expectedStatus, status);
            return;
        }
        // Navigate through Chromium so the request uses its negotiated Windows credentials.
        var response = await page.GotoAsync($"{baseUrl}{path}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.NotNull(response);
        Assert.Equal(expectedStatus, response.Status);
    }

    private Task<IPage> NewPageAsync() => browser!.NewPageAsync(new BrowserNewPageOptions
    {
        ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
    });
}

public sealed class StagingRoleBrowserFactAttribute : FactAttribute
{
    public StagingRoleBrowserFactAttribute(string role)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_AUTH_BROWSER_TESTS"), "1", StringComparison.Ordinal)
            || !string.Equals(Environment.GetEnvironmentVariable("STAGING_BROWSER_ROLE"), role, StringComparison.OrdinalIgnoreCase))
            Skip = $"Set RUN_AUTH_BROWSER_TESTS=1, BROWSER_BASE_URL, and STAGING_BROWSER_ROLE={role} while running Chromium as that staging Windows identity.";
    }
}