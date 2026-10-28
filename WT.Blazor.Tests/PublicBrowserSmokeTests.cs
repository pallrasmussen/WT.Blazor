using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace WT.Blazor.Tests;

[Collection("Browser fixture")]
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

    [BrowserFact]
    public async Task PublicAndStaffPagesSupportMobileReflowLargeTextAndWcagChecks()
    {
        var reportPath = Environment.GetEnvironmentVariable("BROWSER_A11Y_RESULTS_PATH") ?? Path.Combine(Path.GetTempPath(), "wt-a11y");
        Directory.CreateDirectory(reportPath);
        foreach (var culture in new[] { "fo", "en" })
            foreach (var width in new[] { 1280, 320 })
                foreach (var route in Environment.GetEnvironmentVariable("RUN_MUTATING_BROWSER_TESTS") == "1"
                    ? new[] { "/", "/internal?review=true", "/internal?new=true", "/internal/settings" } : new[] { "/" })
                {
                    var page = await browser!.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 900 } });
                    try
                    {
                        await page.GotoAsync($"{baseUrl}/culture/{culture}?returnUrl={Uri.EscapeDataString(route)}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                        await Assertions.Expect(page.Locator("main")).ToBeVisibleAsync();
                        if (width == 320) await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = "html { font-size: 200% !important; }" });
                        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"), $"Horizontal overflow at {culture} {width}px {route}");
                        var result = await page.RunAxe(new AxeRunOptions
                        {
                            RunOnly = new RunOnlyOptions { Type = "tag", Values = new List<string> { "wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa" } }
                        });
                        var name = $"{culture}-{width}-{route.Replace('/', '_').Replace('?', '_').Replace('=', '_')}";
                        await File.WriteAllTextAsync(Path.Combine(reportPath, $"{name}.json"), JsonSerializer.Serialize(result));
                        if (result.Violations.Any()) await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(reportPath, $"{name}.png"), FullPage = true });
                        Assert.True(!result.Violations.Any(), $"{culture} {width}px {route}: {JsonSerializer.Serialize(result.Violations)}");
                    }
                    finally { await page.CloseAsync(); }
                }
    }

    [LocalWorkflowBrowserFact]
    public async Task EditingDialogKeepsKeyboardFocusAndReturnsItOnEscape()
    {
        var page = await browser!.NewPageAsync();
        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var edit = page.Locator(".admin-actions button.btn-teal").First;
        await edit.ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        for (var index = 0; index < 30; index++)
        {
            await page.Keyboard.PressAsync(index % 2 == 0 ? "Tab" : "Shift+Tab");
            Assert.True(await dialog.EvaluateAsync<bool>("element => element.contains(document.activeElement)"));
        }
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(edit).ToBeFocusedAsync();
    }

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, ExecutablePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH") });
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
