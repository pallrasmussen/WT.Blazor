using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace WT.Blazor.Tests;

// Opt in only against the disposable Development fixture created by the local runner.
[Collection("Browser fixture")]
public class LocalWorkflowBrowserTests : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private readonly string baseUrl = Environment.GetEnvironmentVariable("BROWSER_BASE_URL")?.TrimEnd('/') ?? "";

    [LocalWorkflowBrowserFact]
    public async Task ComparisonsLargeChangeConfirmationAndGuidancePublicationWorkTogether()
    {
        var page = await browser!.NewPageAsync();
        var runId = Guid.NewGuid().ToString("N")[..8];
        var northName = $"Browser MRI North {runId}";
        var southName = $"Browser MRI South {runId}";
        var childName = $"Browser MRI Children {runId}";
        var comparisonCode = $"SMOKE-MRI-{runId}";
        await page.GotoAsync($"{baseUrl}/culture/en?returnUrl=/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await CreateAsync(page, northName, "Klaksvík Hospital", 4, "Routine adult referrals", comparisonCode);
        await CreateAsync(page, southName, "Suðuroy Hospital", 6, "Routine adult referrals", comparisonCode);
        await CreateAsync(page, childName, "National Hospital", 2, "Paediatric referrals", comparisonCode);
        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var comparison = page.Locator(".comparison-group").Filter(new LocatorFilterOptions { HasText = northName });
        await Assertions.Expect(comparison.Locator(".comparison-estimates li")).ToHaveCountAsync(2);
        await Assertions.Expect(comparison).ToContainTextAsync("28 days");
        await Assertions.Expect(comparison).ToContainTextAsync("42 days");
        await Assertions.Expect(comparison).Not.ToContainTextAsync("Paediatric");
        Assert.DoesNotContain("SMOKE-MRI", await page.Locator("body").InnerTextAsync());
        await page.SetViewportSizeAsync(320, 900);
        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = "html { font-size: 200% !important; }" });
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
        var accessibility = await page.RunAxe();
        Assert.True(!accessibility.Violations.Any(), System.Text.Json.JsonSerializer.Serialize(accessibility.Violations));
        await page.SetViewportSizeAsync(1280, 900);

        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator(".admin-row").Filter(new LocatorFilterOptions { HasText = northName }).Locator(".admin-actions button.btn-teal").ClickAsync();
        await page.Locator("#edit-wait").FillAsync("40");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
        var confirm = page.Locator(".change-preview button.btn-teal");
        await Assertions.Expect(confirm).ToBeDisabledAsync();
        await page.Locator("#confirm-large-edit").CheckAsync();
        await Assertions.Expect(confirm).ToBeEnabledAsync();
        await page.Locator("#edit-wait").FillAsync("41");
        await page.Locator("#edit-unit").FocusAsync();
        await Assertions.Expect(confirm).ToBeDisabledAsync();
        await page.Locator("#confirm-large-edit").CheckAsync();
        await confirm.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        await page.GotoAsync($"{baseUrl}/internal/settings", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#referral-fo").FillAsync("Góðkend royndarvegleiðing");
        await page.Locator("#referral-en").FillAsync($"Approved fixture referral instructions {runId}");
        await page.Locator("#contact-fo").FillAsync("Royndarsamband");
        await page.Locator("#contact-en").FillAsync("Fixture referral office");
        await page.Locator("#publish-guidance").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save settings", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".status-banner")).ToBeVisibleAsync();
        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.Locator(".patient-next-steps")).ToContainTextAsync("Approved fixture referral instructions");
        await Assertions.Expect(page.Locator(".patient-next-steps")).ToContainTextAsync("Fixture referral office");
        await page.GotoAsync($"{baseUrl}/culture/fo?returnUrl=/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.Locator(".patient-next-steps")).ToContainTextAsync("Góðkend royndarvegleiðing");
    }

    [LocalWorkflowBrowserFact]
    public async Task StaffCanPublishARangeAndEditMetadataWithoutLosingEitherBound()
    {
        var page = await browser!.NewPageAsync();
        var name = $"Browser Range {Guid.NewGuid():N}";
        await page.GotoAsync($"{baseUrl}/culture/en?returnUrl=/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await CreateAsync(page, name, "Klaksvík Hospital", 6, "Routine referrals", "", upper: 10);
        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var card = page.Locator(".waiting-card").Filter(new LocatorFilterOptions { HasText = name });
        await Assertions.Expect(card.Locator(".wait-value")).ToHaveTextAsync("6–10 weeks");
        await card.Locator(".graph-link").ClickAsync();
        await Assertions.Expect(page.Locator(".section-heading")).ToContainTextAsync("6–10 weeks");
        await Assertions.Expect(page.Locator(".public-panel")).ToContainTextAsync("Ranges are not plotted as single values");
        await page.GotoAsync($"{baseUrl}/internal", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator(".admin-row").Filter(new LocatorFilterOptions { HasText = name }).Locator(".admin-actions button.btn-teal").ClickAsync();
        await Assertions.Expect(page.Locator("#edit-wait-upper")).ToHaveValueAsync("10");
        await page.Locator("#edit-note").FillAsync("Routine adult referrals");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
        await page.Locator(".change-preview button.btn-teal").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.Locator(".waiting-card").Filter(new LocatorFilterOptions { HasText = name }).Locator(".wait-value")).ToHaveTextAsync("6–10 weeks");
    }

    private async Task CreateAsync(IPage page, string name, string hospital, int weeks, string conditions, string comparisonCode, int? upper = null)
    {
        await page.GotoAsync($"{baseUrl}/internal?new=true", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("#new-hospital").SelectOptionAsync(new SelectOptionValue { Label = hospital });
        if (hospital == "National Hospital") await page.Locator("#new-department").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await page.Locator("#new-name").FillAsync(name);
        await page.Locator("#new-estimate-status").SelectOptionAsync("Available");
        await Assertions.Expect(page.Locator("#new-wait")).ToBeEnabledAsync();
        await page.Locator("#new-wait").FillAsync(weeks.ToString());
        await page.Locator("#new-unit").SelectOptionAsync("Vikur");
        if (upper is { } bound) await page.Locator("#new-wait-upper").FillAsync(bound.ToString());
        await page.Locator("#new-referral-endpoint").SelectOptionAsync("Investigation");
        await page.Locator("#new-comparison-code").FillAsync(comparisonCode);
        await page.Locator("#new-responsible-department").FillAsync("Imaging");
        await page.Locator("#new-note").FillAsync(conditions);
        await page.Locator(".create-box button.btn-teal").ClickAsync();
        await Assertions.Expect(page.Locator(".create-box")).ToHaveCountAsync(0);
    }

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH")
        });
    }
    public async Task DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }
}

public sealed class LocalWorkflowBrowserFactAttribute : FactAttribute
{
    public LocalWorkflowBrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_BROWSER_TESTS") != "1"
            || Environment.GetEnvironmentVariable("RUN_MUTATING_BROWSER_TESTS") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BROWSER_BASE_URL")))
            Skip = "Run against the disposable Development fixture with RUN_BROWSER_TESTS=1, RUN_MUTATING_BROWSER_TESTS=1 and BROWSER_BASE_URL.";
    }
}

[CollectionDefinition("Browser fixture", DisableParallelization = true)]
public sealed class BrowserFixtureCollection;
