using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Serilog;
using System.Globalization;
using System.Threading.RateLimiting;
using WT.Blazor.Components;
using WT.Blazor.Data;
using WT.Blazor.Models;
using WT.Blazor.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Behind IIS/corporate networks, brief WebSocket hiccups shouldn't tear the whole
        // circuit down and blank the page - give reconnection a much longer window.
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
    });

// Tolerate slower/intermittent WebSocket keep-alives (common behind IIS/proxies) instead of
// the SignalR default 30s client timeout, which was causing circuits to drop after ~20s idle.
builder.Services.Configure<HubOptions>(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(120);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { new CultureInfo("fo"), new CultureInfo("en") };
    options.DefaultRequestCulture = new RequestCulture("fo");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});

// Persist Data Protection keys to disk so cookies (antiforgery, auth, etc.) survive
// app pool restarts/recycles instead of becoming undecryptable every time the process restarts.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")))
    .SetApplicationName("WT.Blazor");

WindowsAuthenticationConfiguration.Configure(
    builder.Services,
    Environment.GetEnvironmentVariable(WindowsAuthenticationConfiguration.IisPhysicalPathEnvironmentVariable),
    builder.Environment.IsDevelopment());

var localMachine = Environment.MachineName;
var adGroups = builder.Configuration.GetSection("AdGroups").Get<AdGroupsOptions>() ?? new();
adGroups.ApplyDefaults(localMachine);
builder.Services.Configure<AdGroupsOptions>(builder.Configuration.GetSection("AdGroups"));
builder.Services.PostConfigure<AdGroupsOptions>(options => options.ApplyDefaults(localMachine));
builder.Services.AddSingleton<IValidateOptions<AdGroupsOptions>, AdGroupsOptionsValidator>();
builder.Services.AddOptions<AdGroupsOptions>().ValidateOnStart();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ViewerAccess, policy => policy.RequireRole(adGroups.Viewer, adGroups.Contributor, adGroups.Admin));
    options.AddPolicy(Policies.SuperAdminAccess, policy => policy.RequireAssertion(context => Policies.IsSuperAdmin(context.User, adGroups.Admin)));
    options.AddPolicy(Policies.AdminAccess, policy => policy.RequireRole(adGroups.Contributor, adGroups.Admin));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=App_Data/wt-blazor.db";
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("exports", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.Configure<AuditRetentionOptions>(builder.Configuration.GetSection("AuditRetention"));
builder.Services.AddOptions<ExportOptions>()
    .Bind(builder.Configuration.GetSection("Exports"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<WaitingTimeReadService>();
builder.Services.AddScoped<IWaitingTimeCatalogService>(services => services.GetRequiredService<WaitingTimeReadService>());
builder.Services.AddScoped<IHistoryReadService>(services => services.GetRequiredService<WaitingTimeReadService>());
builder.Services.AddScoped<IOperationsReadService>(services => services.GetRequiredService<WaitingTimeReadService>());
builder.Services.AddScoped<ITrendReadService>(services => services.GetRequiredService<WaitingTimeReadService>());
builder.Services.AddScoped<IWaitingTimeManagementService, WaitingTimeManagementService>();
builder.Services.AddSingleton<PortalSettingsChangeNotifier>();
builder.Services.AddScoped<IPortalSettingsService, PortalSettingsService>();
builder.Services.AddScoped<IHistoryExportService, HistoryExportService>();
builder.Services.AddScoped<ITrendExportService, TrendExportService>();
builder.Services.AddScoped<ITrendReportService, TrendReportService>();
builder.Services.AddHostedService<ExportTemporaryFileCleanupService>();
var deploymentManifest = DeploymentManifest.Load(Path.Combine(builder.Environment.ContentRootPath, "deployment-manifest.json"));
builder.Services.AddSingleton(deploymentManifest);

var app = builder.Build();

ExportTemporaryStorage.EnsureAvailable(app.Services.GetRequiredService<IOptions<ExportOptions>>().Value);
var configuredGroups = app.Services.GetRequiredService<IOptions<AdGroupsOptions>>().Value;
app.Logger.LogInformation("Configured Windows security groups: Viewer={ViewerGroup}, Contributor={ContributorGroup}, Admin={AdminGroup}", configuredGroups.Viewer, configuredGroups.Contributor, configuredGroups.Admin);
app.Logger.LogInformation("Deployment manifest: BuildVersion={BuildVersion}, CommitSha={CommitSha}, PublishedAtUtc={PublishedAtUtc}", deploymentManifest.BuildVersion, deploymentManifest.CommitSha, deploymentManifest.PublishedAtUtc);

using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider,
        app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedData:EnableDemoData"));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseSerilogRequestLogging();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (app.Configuration.GetValue<HostingOptions>("Hosting")?.RedirectToHttps == true)
    app.UseHttpsRedirection();
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapGet("/diagnostics/deployment", () => Results.Ok(deploymentManifest))
    .RequireAuthorization(Policies.SuperAdminAccess);
app.MapGet("/culture/{culture}", (string culture, string? returnUrl, HttpResponse response) =>
{
    if (culture is not ("fo" or "en")) return Results.BadRequest();
    response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)));
    return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHistoryExport("/api/history/export/csv", static (service, query, cancellationToken) => service.CreateCsvAsync(query, cancellationToken));
app.MapHistoryExport("/api/history/export", static (service, query, cancellationToken) => service.CreateExcelAsync(query, cancellationToken));
app.MapHistoryExport("/api/history/export/pdf", static (service, query, cancellationToken) => service.CreatePdfAsync(query, cancellationToken));
app.MapTrendPdfExport("/api/trend/{id:int}/export/pdf");

app.Run();

public partial class Program;
