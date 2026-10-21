using Microsoft.Playwright;
using Xunit;

namespace WT.Blazor.Tests;

public class PublicBrowserSmokeTests : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private readonly string baseUrl = Environment.GetEnvironmentVariable("BROWSER_BASE_URL")?.TrimEnd('/') ?? "";

    [BrowserFact]
    public async Task PublicPageSupportsSearchAndAdministrationNavigation()
    {
        var page = await browser!.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#hospital")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("a.internal-link")).ToHaveAttributeAsync("href", "/internal");

        var searchLabel = (await page.Locator("label[for='search']").InnerTextAsync()).Trim();
        Assert.False(string.IsNullOrWhiteSpace(searchLabel));
        var searchBox = page.GetByLabel(searchLabel);
        await searchBox.FillAsync("__browser-smoke-no-match__");
        await Assertions.Expect(page.Locator(".empty-state")).ToBeVisibleAsync();
        await page.Locator(".refresh-results").ClickAsync();
        await Assertions.Expect(page.Locator(".refresh-results")).ToBeEnabledAsync();
        await Assertions.Expect(searchBox).ToHaveValueAsync("__browser-smoke-no-match__");
        await Assertions.Expect(page.Locator(".empty-state")).ToBeVisibleAsync();

        await searchBox.FillAsync("");
        var trendLink = page.Locator("a.graph-link").First;
        await Assertions.Expect(trendLink).ToBeVisibleAsync();
        await trendLink.ClickAsync();
        await page.WaitForURLAsync("**/gongd/**");

        await Assertions.Expect(page.Locator("#trend-period")).ToBeVisibleAsync();
        await page.Locator("#trend-period").SelectOptionAsync("90");
        await Assertions.Expect(page.Locator("svg.chart-svg")).ToBeVisibleAsync();
        var chartAccessibleName = await page.Locator("svg.chart-svg[role='img']").GetAttributeAsync("aria-label");
        Assert.False(string.IsNullOrWhiteSpace(chartAccessibleName));
        var exportLink = page.Locator(".trend-actions a");
        await Assertions.Expect(exportLink).ToHaveAttributeAsync("href", new System.Text.RegularExpressions.Regex(@"/api/trend/\d+/export/pdf\?periodDays=90$"));
        var exportLinkName = (await exportLink.InnerTextAsync()).Trim();
        Assert.False(string.IsNullOrWhiteSpace(exportLinkName));
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = exportLinkName })).ToHaveCountAsync(1);
        await page.Locator("#trend-period").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await exportLink.EvaluateAsync<bool>("element => element === document.activeElement"));

        var download = await page.RunAndWaitForDownloadAsync(async () => await exportLink.ClickAsync());
        Assert.EndsWith(".pdf", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("_90d.pdf", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
    }

    [BrowserFact]
    public async Task PublicPageProvidesKeyboardNavigationAndAccessibleLandmarks()
    {
        var page = await browser!.NewPageAsync();

        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var skipLink = page.Locator("a.skip-link");
        await Assertions.Expect(skipLink).ToHaveAttributeAsync("href", "#public-content");
        // Blazor focuses the page heading on navigation. Start this tab-order
        // assertion at the beginning of the document rather than at that heading.
        await page.Locator("body").EvaluateAsync("element => { element.tabIndex = -1; element.focus(); element.removeAttribute('tabindex'); }");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(skipLink).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#public-content")).ToBeFocusedAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Main)).ToHaveCountAsync(1);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("img:not([alt])")).ToHaveCountAsync(0);

        foreach (var input in await page.Locator("input:not([type='hidden'])").AllAsync())
        {
            var inputId = await input.GetAttributeAsync("id");
            var accessibleName = await input.GetAttributeAsync("aria-label");
            Assert.True(
                !string.IsNullOrWhiteSpace(accessibleName)
                || (!string.IsNullOrWhiteSpace(inputId) && await page.Locator($"label[for='{inputId}']").CountAsync() == 1),
                $"Input '{inputId}' must have an accessible name.");
        }
    }

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }

}

public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_BROWSER_TESTS"), "1", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BROWSER_BASE_URL")))
            Skip = "Set RUN_BROWSER_TESTS=1 and BROWSER_BASE_URL to a running test site after installing Playwright Chromium, or use tests/Run-LocalBrowserSmoke.ps1.";
    }
}
