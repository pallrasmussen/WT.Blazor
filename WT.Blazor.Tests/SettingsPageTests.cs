using Bunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using WT.Blazor.Components.Pages;
using WT.Blazor.Data;
using WT.Blazor.Localization;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class SettingsPageTests : BunitContext
{
    [Fact]
    public async Task EnablesSaveOnlyAfterASettingChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new PortalSettingsService(db, new PortalSettingsChangeNotifier(), TimeProvider.System);
        Services.AddSingleton<IPortalSettingsService>(service);

        var cut = Render<Settings>();

        Assert.True(cut.Find("button.btn-teal").HasAttribute("disabled"));
        cut.Find("#green-months").Change("4");
        Assert.False(cut.Find("button.btn-teal").HasAttribute("disabled"));
        Assert.Contains("dirty-note", cut.Markup);
    }

    [Fact]
    public async Task DisplaysSavedSettingsAuditEntry()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fo");
        try
        {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new PortalSettingsService(db, new PortalSettingsChangeNotifier(), TimeProvider.System);
        await service.SaveAsync(new PortalSettings
        {
            GreenMaximumAgeMonths = 2,
            YellowMaximumAgeMonths = 5,
            LongWaitMonths = 10
        }, "test-user");
        Services.AddSingleton<IPortalSettingsService>(service);

        var cut = Render<Settings>();

        Assert.Contains("Seinastu broytingar", cut.Markup);
        Assert.Contains("test-user", cut.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void InvalidThresholdsShowAnErrorWithoutSaving()
    {
        var service = new RecordingPortalSettingsService();
        Services.AddSingleton<IPortalSettingsService>(service);

        var cut = Render<Settings>();

        cut.Find("#green-months").Change("8");
        cut.Find("#yellow-months").Change("4");
        cut.Find("button.btn-teal").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(0, service.SaveCalls);
            Assert.Single(cut.FindAll("[role='alert']"));
        });
    }

    [Fact]
    public void SuccessfulSaveClearsTheDirtyStateAndShowsConfirmation()
    {
        var service = new RecordingPortalSettingsService();
        Services.AddSingleton<IPortalSettingsService>(service);

        var cut = Render<Settings>();
        cut.Find("#green-months").Change("4");
        cut.Find("button.btn-teal").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, service.SaveCalls);
            Assert.Empty(cut.FindAll(".dirty-note"));
            Assert.Single(cut.FindAll("[role='status']"));
        });
    }

    [Fact]
    public void FailedSaveShowsAnErrorAndKeepsTheChanges()
    {
        var service = new RecordingPortalSettingsService(throwOnSave: true);
        Services.AddLogging();
        Services.AddSingleton<IPortalSettingsService>(service);

        var cut = Render<Settings>();
        cut.Find("#green-months").Change("4");
        cut.Find("button.btn-teal").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, service.SaveCalls);
            Assert.Contains(AppText.Get("SettingsSaveFailed"), cut.Markup);
            Assert.NotEmpty(cut.FindAll(".dirty-note"));
        });
    }

    [Fact]
    public void ConcurrentSaveShowsReloadGuidanceAndKeepsTheChanges()
    {
        var service = new RecordingPortalSettingsService(throwOnConcurrencyConflict: true);
        Services.AddLogging();
        Services.AddSingleton<IPortalSettingsService>(service);

        var cut = Render<Settings>();
        cut.Find("#green-months").Change("4");
        cut.Find("button.btn-teal").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(AppText.Get("SettingsConcurrencyConflict"), cut.Markup);
            Assert.NotEmpty(cut.FindAll(".dirty-note"));
        });
    }

    private sealed class RecordingPortalSettingsService : IPortalSettingsService
    {
        private readonly bool throwOnSave;
        private readonly bool throwOnConcurrencyConflict;

        public RecordingPortalSettingsService(bool throwOnSave = false, bool throwOnConcurrencyConflict = false)
        {
            this.throwOnSave = throwOnSave;
            this.throwOnConcurrencyConflict = throwOnConcurrencyConflict;
        }

        public int SaveCalls { get; private set; }

        public Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PortalSettings());

        public Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            if (throwOnConcurrencyConflict) throw new PortalSettingsConcurrencyException();
            if (throwOnSave) throw new InvalidOperationException("Simulated save failure.");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PortalSettingsAuditEntry>>([]);
    }
}