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
        var exportLinkName = (await exportLink.InnerTextAsync()).Trim();
        Assert.False(string.IsNullOrWhiteSpace(exportLinkName));
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = exportLinkName })).ToHaveCountAsync(1);
        await page.Locator("#trend-period").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await exportLink.EvaluateAsync<bool>("element => element === document.activeElement"));

        var download = await page.RunAndWaitForDownloadAsync(async () => await exportLink.ClickAsync());
        Assert.EndsWith(".pdf", download.SuggestedFilename, StringComparison.OrdinalIgnoreCase);
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
            Skip = "Set RUN_BROWSER_TESTS=1 and BROWSER_BASE_URL to an IIS staging URL after installing Playwright Chromium.";
    }
}