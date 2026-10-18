using Bunit;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Components.Pages;
using WT.Blazor.Localization;
using WT.Blazor.Models;
using WT.Blazor.Services;
using Xunit;

namespace WT.Blazor.Tests;

public class OperationsPageTests : BunitContext
{
    [Fact]
    public void ShowsNoBackupWhenTheConfiguredDirectoryHasNoDatabaseBackup()
    {
        var backupPath = CreateBackupDirectory();
        try
        {
            var reader = new RecordingOperationsReader();
            var cut = RenderOperations(backupPath, reader);

            cut.WaitForAssertion(() =>
            {
                Assert.Contains(AppText.Get("NoBackup"), cut.Markup);
                Assert.Equal((30, PortalSettings.DefaultGreenMaximumAgeMonths, PortalSettings.DefaultLongWaitMonths), reader.Request);
            });
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }

    [Fact]
    public void ShowsTheMostRecentConfiguredDatabaseBackup()
    {
        var backupPath = CreateBackupDirectory();
        try
        {
            var oldBackup = Path.Combine(backupPath, "wt-blazor-old.db");
            var currentBackup = Path.Combine(backupPath, "wt-blazor-current.db");
            File.WriteAllText(oldBackup, "old");
            File.WriteAllText(currentBackup, "current");
            File.SetLastWriteTimeUtc(oldBackup, DateTime.UtcNow.AddDays(-2));
            File.SetLastWriteTimeUtc(currentBackup, DateTime.UtcNow.AddDays(-1));

            var cut = RenderOperations(backupPath, new RecordingOperationsReader());

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("wt-blazor-current.db", cut.Markup);
                Assert.DoesNotContain("wt-blazor-old.db", cut.Markup);
            });
        }
        finally
        {
            Directory.Delete(backupPath, recursive: true);
        }
    }

    private IRenderedComponent<Operations> RenderOperations(string backupPath, RecordingOperationsReader reader)
    {
        Services.Configure<BackupOptions>(options => options.Path = backupPath);
        Services.Configure<AuditRetentionOptions>(options => options.ReviewAfterDays = 30);
        Services.AddSingleton<IOperationsReadService>(reader);
        Services.AddSingleton<IPortalSettingsService>(new DefaultPortalSettingsService());
        Services.AddSingleton<PortalSettingsChangeNotifier>();
        Services.AddSingleton(TimeProvider.System);

        return Render<Operations>();
    }

    private static string CreateBackupDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wt-blazor-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class RecordingOperationsReader : IOperationsReadService
    {
        public (int RetentionDays, int GreenMaximumAgeMonths, int LongWaitMonths)? Request { get; private set; }

        public Task<OperationsSummary> GetOperationsSummaryAsync(int retentionDays, int greenMaximumAgeMonths, int longWaitMonths)
        {
            Request = (retentionDays, greenMaximumAgeMonths, longWaitMonths);
            return Task.FromResult(new OperationsSummary(2, 3, 1, 4, 5));
        }
    }

    private sealed class DefaultPortalSettingsService : IPortalSettingsService
    {
        public Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PortalSettings());
        public Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PortalSettingsAuditEntry>>([]);
    }
}