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
        var response = await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.NotNull(response);
        Assert.Equal(403, response.Status);
        // Missing controls alone would also pass for a failed sign-in or error page.
        await Assertions.Expect(page.Locator(".access-message")).ToBeVisibleAsync();

        Assert.Equal(0, await page.Locator("#show-inactive").CountAsync());
        Assert.Equal(0, await page.Locator(".admin-actions").CountAsync());
        var export = await page.GotoAsync($"{baseUrl}/api/history/export/csv");
        Assert.NotNull(export);
        Assert.Equal(403, export.Status);
    }

    [StagingRoleBrowserFact("Contributor")]
    public async Task ContributorCanEditButCannotUseAdminOnlyControls()
    {
        var page = await NewPageAsync();
        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator(".admin-actions .btn-teal").First).ToBeVisibleAsync();
        Assert.Equal(0, await page.Locator("#show-inactive").CountAsync());
        Assert.Equal(0, await page.Locator("button.btn-deactivate").CountAsync());
    }

    [StagingRoleBrowserFact("Admin")]
    public async Task AdminSeesLifecycleControls()
    {
        var page = await NewPageAsync();
        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(page.Locator("#show-inactive")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button.btn-deactivate").First).ToBeVisibleAsync();
    }

    public async Task InitializeAsync()
    {
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
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BROWSER_BASE_URL"))
            || !string.Equals(Environment.GetEnvironmentVariable("STAGING_BROWSER_ROLE"), role, StringComparison.OrdinalIgnoreCase))
            Skip = $"Set RUN_AUTH_BROWSER_TESTS=1, BROWSER_BASE_URL, and STAGING_BROWSER_ROLE={role} while running Chromium as that staging Windows identity.";
    }
}