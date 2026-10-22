using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WT.Blazor.Data;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Security.Claims;
using WT.Blazor.Components.Pages;
using WT.Blazor.Localization;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class InternalPageTests : BunitContext
{
    [Theory]
    [InlineData("create")]
    [InlineData("edit")]
    [InlineData("quick")]
    public void OverflowingDurationsAreRejectedBeforeSaving(string mode)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager, openCreateForm: mode == "create", reviewQueue: mode == "quick");
        var prefix = mode == "create" ? "new" : mode == "edit" ? "edit" : "quick";
        if (mode == "create")
        {
            cut.Find("#new-hospital").Change(DisplayNames.For(Hospital.Klaksvikar));
            cut.Find("#new-name").Change("Too large");
        }
        else if (mode == "edit")
            cut.FindAll("button").Single(button => button.TextContent == AppText.Get("Edit")).Click();
        else cut.Find(".admin-list").KeyDown("ArrowDown");
        var suffix = mode == "quick" ? "-1" : "";
        cut.Find($"#{prefix}-unit{suffix}").Change(WaitUnit.Manadir.ToString());
        cut.Find($"#{prefix}-wait{suffix}").Change("71582789");
        if (mode == "create") cut.Find("button.btn-teal").Click();
        else cut.FindAll("button").Single(button => button.TextContent == AppText.Get("SaveChanges")).Click();
        cut.WaitForAssertion(() => Assert.Contains(string.Format(AppText.Get("MaximumWaitValidation"), 71582788), cut.Markup));
        Assert.Equal("71582788", cut.Find($"#{prefix}-wait{suffix}").GetAttribute("max"));
        Assert.Null(manager.CreateRequest);
        Assert.Null(manager.UpdateRequest);
    }

    [Fact]
    public void HospitalFilterIncludesSharedAndAllWaitingListsOptions()
    {
        var cut = RenderInternal(new WaitingTimeManager());
        var options = cut.FindAll("#admin-hospital option");

        Assert.Contains(options, option =>
            option.GetAttribute("value") == ""
            && option.TextContent == AppText.Get("AllWaitingLists"));
        Assert.Contains(options, option =>
            option.GetAttribute("value") == Hospital.Sjukrahusverkid.ToString()
            && option.TextContent == AppText.Get("SharedWaitingLists"));
    }

    [Fact]
    public void CreateFormIncludesSharedWaitingListsOption()
    {
        var cut = RenderInternal(new WaitingTimeManager(), openCreateForm: true);

        Assert.Contains(cut.FindAll("#new-hospital option"), option =>
            option.GetAttribute("value") == DisplayNames.For(Hospital.Sjukrahusverkid)
            && option.TextContent == AppText.Get("SharedWaitingLists"));
    }

    [Fact]
    public void SharedWaitingListsShortcutSelectsTheSharedFilter()
    {
        var cut = RenderInternal(new WaitingTimeManager());

        cut.FindAll(".filter-presets button")
            .Single(button => button.TextContent == AppText.Get("SharedWaitingLists"))
            .Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(Hospital.Sjukrahusverkid.ToString(), cut.Find("#admin-hospital").GetAttribute("value")));
        Assert.Contains("preset=shared", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri);
    }

    [Fact]
    public void ShowInactiveSwitchesFromActiveToInactiveWaitingTimes()
    {
        const string adminGroup = @"TEST-SERVER\WT-Admins";
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(adminGroup));
        Services.Configure<AdGroupsOptions>(options => options.Admin = adminGroup);
        Services.AddSingleton<IWaitingTimeCatalogService>(new CatalogService());
        Services.AddSingleton<IHistoryReadService>(new HistoryReader());
        Services.AddSingleton<IWaitingTimeManagementService>(new WaitingTimeManager());
        Services.AddSingleton<IPortalSettingsService>(new PortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());
        Services.AddRazorComponents();
        Services.AddSingleton(TimeProvider.System);

        var cut = Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<Internal>());

        cut.WaitForAssertion(() => Assert.Contains("Active waiting time", cut.Markup));
        Assert.DoesNotContain("Inactive waiting time", cut.Markup);

        cut.Find("#show-inactive").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Active waiting time", cut.Markup);
            Assert.Contains("Inactive waiting time", cut.Markup);
        });
    }

    [Fact]
    public void CreateFormValidatesRequiredFieldsBeforeSaving()
    {
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager, openCreateForm: true);

        cut.Find("button.btn-teal").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Null(manager.CreateRequest);
            Assert.Single(cut.FindAll(".login-error"));
        });
    }

    [Fact]
    public void CreateFormSendsTrimmedValuesToManagementService()
    {
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager, openCreateForm: true);

        cut.Find("#new-hospital").Change(DisplayNames.For(Hospital.Klaksvikar));
        cut.Find("#new-name").Change("  New waiting time  ");
        cut.Find("#new-wait").Change("4");
        cut.Find("#new-unit").Change(WaitUnit.Vikur.ToString());
        cut.Find("#new-note").Change("  Note  ");
        cut.Find("button.btn-teal").Click();

        cut.WaitForAssertion(() =>
        {
            var request = Assert.IsType<WaitingTimeCreate>(manager.CreateRequest);
            Assert.Equal("New waiting time", request.Name);
            Assert.Equal(Hospital.Klaksvikar, request.Hospital);
            Assert.Null(request.Department);
            Assert.Equal(4, request.WaitValue);
            Assert.Equal(WaitUnit.Vikur, request.Unit);
            Assert.Equal("Note", request.Note);
        });
    }

    [Fact]
    public void DeactivationSendsExpectedLifecycleCommand()
    {
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.btn-deactivate")));
        cut.Find("button.btn-deactivate").Click();
        cut.Find("#deactivation-reason").Input("Duplicate");
        cut.Find(".modal-actions .btn-danger").Click();

        cut.WaitForAssertion(() =>
        {
            var request = Assert.IsType<(int Id, int Version, string? Reason)>(manager.DeactivationRequest);
            Assert.Equal((1, 1, "Duplicate"), request);
        });
    }

    [Fact]
    public void ReactivationSendsExpectedLifecycleCommand()
    {
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager);

        cut.Find("#show-inactive").Change(true);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".admin-row.inactive")));
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("Activate")).Click();

        cut.WaitForAssertion(() => Assert.Equal((2, 1), manager.ReactivationRequest));
    }

    [Fact]
    public void EditConflictIsShownToTheAdministrator()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var manager = new WaitingTimeManager { UpdateException = new WaitingTimeConcurrencyException() };
        var cut = RenderInternal(manager);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button"), button => button.TextContent == AppText.Get("Edit")));
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("Edit")).Click();
        cut.Find("#edit-wait").Change("9");
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("SaveChanges")).Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button"), button => button.TextContent == AppText.Get("ConfirmSave")));
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("ConfirmSave")).Click();

        cut.WaitForAssertion(() => Assert.Contains(AppText.Get("WaitingTimeConflict"), cut.Markup));
    }

    [Fact]
    public void ReviewQueueMarksTheSelectedWaitingTimeReviewed()
    {
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager, reviewQueue: true);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button"), button => button.TextContent == AppText.Get("MarkReviewed")));
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("MarkReviewed")).Click();

        cut.WaitForAssertion(() => Assert.Equal((1, 1), manager.ReviewRequest));
    }

    [Fact]
    public void ReviewQueueKeyboardOpensQuickEditAndSavesConfirmedChange()
    {
        var manager = new WaitingTimeManager();
        var cut = RenderInternal(manager, reviewQueue: true);

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".admin-list")));
        cut.Find(".admin-list").KeyDown("ArrowDown");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#quick-wait-1")));
        cut.Find("#quick-wait-1").Change("5");
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("SaveChanges")).Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button"), button => button.TextContent == AppText.Get("ConfirmSave")));
        cut.FindAll("button").Single(button => button.TextContent == AppText.Get("ConfirmSave")).Click();

        cut.WaitForAssertion(() => Assert.Equal(5, manager.UpdateRequest?.WaitValue));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoUsersCanReloadAndRetryAfterASaveTimeConflict(bool quick)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        await firstDb.Database.EnsureCreatedAsync();
        var clock = new OldTimeProvider();
        WT.Blazor.Services.WaitingTimeManagementService Manager(ApplicationDbContext db) =>
            new(db, NullLogger<WT.Blazor.Services.WaitingTimeManagementService>.Instance, new HttpContextAccessor(), clock);
        var first = Manager(firstDb);
        var item = await first.CreateAsync(new WaitingTimeCreate("Two-user record", Hospital.Klaksvikar, null, 2, WaitUnit.Vikur, null, "first"));
        await secondDb.WaitingTimes.FindAsync(item.Id);
        var reader = new WaitingTimeReadService(secondDb, clock);
        var cut = RenderInternal(Manager(secondDb), reviewQueue: quick, catalog: reader, history: reader);
        cut.WaitForAssertion(() => Assert.Contains("Two-user record", cut.Markup));
        if (quick) cut.Find(".admin-list").KeyDown("ArrowDown");
        else cut.FindAll("button").Single(button => button.TextContent == AppText.Get("Edit")).Click();
        var input = quick ? $"#quick-wait-{item.Id}" : "#edit-wait";
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(input)));
        cut.Find(input).Change("9");

        await first.UpdateAsync(new WaitingTimeUpdate(item.Id, 1, item.Name, Hospital.Klaksvikar, null, 5, WaitUnit.Vikur, null, "first"));
        void Save()
        {
            cut.FindAll("button").Single(button => button.TextContent == AppText.Get("SaveChanges")).Click();
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button"), button => button.TextContent == AppText.Get("ConfirmSave")));
            cut.FindAll("button").Single(button => button.TextContent == AppText.Get("ConfirmSave")).Click();
        }
        Save();
        cut.WaitForAssertion(() => Assert.Contains(AppText.Get("WaitingTimeConflict"), cut.Markup));
        Assert.Equal("9", cut.Find(input).GetAttribute("value"));
        cut.Find(".reload-latest").Click();
        cut.WaitForAssertion(() => Assert.Equal("5", cut.Find(input).GetAttribute("value")));
        Assert.DoesNotContain(AppText.Get("WaitingTimeConflict"), cut.Markup);
        cut.Find(input).Change("7");
        Save();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(input)));
        var saved = await firstDb.WaitingTimes.AsNoTracking().SingleAsync();
        Assert.Equal(7, saved.WaitValue);
        Assert.Equal(3, saved.Version);
        Assert.Equal(3, await firstDb.WaitingTimeHistoryEntries.CountAsync());
    }

    private sealed class OldTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderInternal(IWaitingTimeManagementService manager, bool openCreateForm = false, bool reviewQueue = false, IWaitingTimeCatalogService? catalog = null, IHistoryReadService? history = null)
    {
        const string adminGroup = @"TEST-SERVER\WT-Admins";
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(adminGroup));
        Services.Configure<AdGroupsOptions>(options => options.Admin = adminGroup);
        Services.AddSingleton<IWaitingTimeCatalogService>(catalog ?? new CatalogService());
        Services.AddSingleton<IHistoryReadService>(history ?? new HistoryReader());
        Services.AddSingleton<IWaitingTimeManagementService>(manager);
        Services.AddSingleton<IPortalSettingsService>(new PortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());
        Services.AddRazorComponents();
        Services.AddSingleton(TimeProvider.System);
        if (openCreateForm || reviewQueue)
        {
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo($"/internal?new={openCreateForm.ToString().ToLowerInvariant()}&review={reviewQueue.ToString().ToLowerInvariant()}");
        }

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<Internal>());
    }

    private sealed class CatalogService : IWaitingTimeCatalogService
    {
        private static readonly List<WaitingTime> Items =
        [
            new() { Id = 1, Name = "Active waiting time", Hospital = Hospital.Klaksvikar, WaitValue = 2, Unit = WaitUnit.Vikur, IsActive = true, UpdatedAt = DateTime.UtcNow.AddMonths(-4) },
            new() { Id = 2, Name = "Inactive waiting time", Hospital = Hospital.Klaksvikar, WaitValue = 3, Unit = WaitUnit.Vikur, IsActive = false }
        ];

        public Task<List<WaitingTime>> GetActiveAsync(Hospital? hospital, Department? department) => Task.FromResult(Items.Where(item => item.IsActive).Select(Clone).ToList());
        public Task<List<WaitingTime>> GetAllAsync(Hospital? hospital, Department? department) => Task.FromResult(Items.Select(Clone).ToList());

        private static WaitingTime Clone(WaitingTime item) => new()
        {
            Id = item.Id,
            Name = item.Name,
            Hospital = item.Hospital,
            Department = item.Department,
            WaitValue = item.WaitValue,
            Unit = item.Unit,
            Note = item.Note,
            IsActive = item.IsActive,
            UpdatedAt = item.UpdatedAt,
            Version = item.Version
        };
    }

    private sealed class TestAuthenticationStateProvider(string adminGroup) : AuthenticationStateProvider
    {
        private readonly AuthenticationState authenticationState = new(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "TEST-SERVER\\test-admin"),
            new Claim(ClaimTypes.Role, adminGroup)
        ], "Test")));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(authenticationState);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WT.Blazor.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class HistoryReader : IHistoryReadService
    {
        public Task<List<WaitingTimeHistoryEntry>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new List<WaitingTimeHistoryEntry>());
        public Task<HistoryPage> GetHistoryPageAsync(HistoryQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new HistoryPage([], 0));
        public Task<List<string>> GetHistoryNamesAsync() => Task.FromResult(new List<string>());
        public Task<List<string>> GetHistoryUsersAsync() => Task.FromResult(new List<string>());
        public Task<Dictionary<int, ReviewInfo>> GetLatestReviewsAsync(IEnumerable<int> waitingTimeIds) => Task.FromResult(new Dictionary<int, ReviewInfo>());
    }

    private sealed class WaitingTimeManager : IWaitingTimeManagementService
    {
        public WaitingTimeCreate? CreateRequest { get; private set; }
        public (int Id, int Version, string? Reason)? DeactivationRequest { get; private set; }
        public (int Id, int Version)? ReactivationRequest { get; private set; }
        public (int Id, int Version)? ReviewRequest { get; private set; }
        public WaitingTimeUpdate? UpdateRequest { get; private set; }
        public Exception? UpdateException { get; init; }

        public Task<WaitingTime> CreateAsync(WaitingTimeCreate create)
        {
            CreateRequest = create;
            return Task.FromResult(new WaitingTime
            {
                Id = 3,
                Name = create.Name,
                Hospital = create.Hospital,
                Department = create.Department,
                WaitValue = create.WaitValue,
                Unit = create.Unit,
                Note = create.Note
            });
        }

        public Task UpdateAsync(WaitingTimeUpdate update)
        {
            if (UpdateException is not null) throw UpdateException;
            UpdateRequest = update;
            return Task.CompletedTask;
        }
        public Task MarkReviewedAsync(int id, int expectedVersion, string username)
        {
            ReviewRequest = (id, expectedVersion);
            return Task.CompletedTask;
        }
        public Task DeactivateAsync(int id, int expectedVersion, string username, string? reason = null)
        {
            DeactivationRequest = (id, expectedVersion, reason);
            return Task.CompletedTask;
        }

        public Task<WaitingTime> ReactivateAsync(int id, int expectedVersion, string username)
        {
            ReactivationRequest = (id, expectedVersion);
            return Task.FromResult(new WaitingTime
            {
                Id = id,
                Name = "Active waiting time",
                Hospital = Hospital.Klaksvikar,
                WaitValue = 2,
                Unit = WaitUnit.Vikur,
                IsActive = true,
                Version = expectedVersion + 1
            });
        }
    }

    private sealed class PortalSettingsService : IPortalSettingsService
    {
        public Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PortalSettings());
        public Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PortalSettingsAuditEntry>>([]);
    }
}
