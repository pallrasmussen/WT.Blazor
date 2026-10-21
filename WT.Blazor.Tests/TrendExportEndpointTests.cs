using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WT.Blazor.Data;
using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

[Collection(WebApplicationFactoryCollection.Name)]
public class TrendExportEndpointTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"wt-blazor-endpoints-{Guid.NewGuid():N}.db");
    private readonly string temporaryPath = Path.Combine(Path.GetTempPath(), $"wt-blazor-exports-{Guid.NewGuid():N}");
    private readonly ExportWebApplicationFactory factory;
    private readonly HttpClient client;

    public TrendExportEndpointTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        Directory.CreateDirectory(temporaryPath);
        factory = new ExportWebApplicationFactory(databasePath, temporaryPath);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    [Fact]
    public async Task TrendExportUsesTheRequestedWindowAndRejectsUnsupportedPeriods()
    {
        int id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = new WaitingTime { Name = "Period test", Hospital = Hospital.Klaksvikar, Unit = WaitUnit.Dagar };
            db.WaitingTimes.Add(item);
            await db.SaveChangesAsync();
            id = item.Id;
            db.WaitingTimeHistoryEntries.AddRange(
                new WaitingTimeHistoryEntry { WaitingTimeId = id, Name = item.Name, Action = HistoryAction.Created, Timestamp = DateTime.UtcNow.AddDays(-200), NewWaitValue = 5, NewUnit = WaitUnit.Dagar },
                new WaitingTimeHistoryEntry { WaitingTimeId = id, Name = item.Name, Action = HistoryAction.Updated, Timestamp = DateTime.UtcNow.AddDays(-1), NewWaitValue = 3, NewUnit = WaitUnit.Dagar });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.GetAsync($"/api/trend/{id}/export/pdf")).StatusCode);
        var recent = await client.GetAsync($"/api/trend/{id}/export/pdf?periodDays=90");
        Assert.Equal(HttpStatusCode.OK, recent.StatusCode);
        Assert.Contains("_90d.pdf", recent.Content.Headers.ContentDisposition?.ToString());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.GetAsync($"/api/trend/{id}/export/pdf?periodDays=365")).StatusCode);
        foreach (var invalid in new[] { "0", "-1", "1", "366", "2147483647", "invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/trend/{id}/export/pdf?periodDays={invalid}")).StatusCode);
    }

    [Fact]
    public async Task PublicTrendExportEndpointReturnsExpectedHttpContracts()
    {
        var (downloadId, limitedId) = await AddWaitingTimesAsync();

        var notFound = await client.GetAsync("/api/trend/999999/export/pdf");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        var download = await client.GetAsync($"/api/trend/{downloadId}/export/pdf");
        var downloadContent = await download.Content.ReadAsStringAsync();
        Assert.True(download.StatusCode == HttpStatusCode.OK, downloadContent);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(download.Content.Headers.ContentLength);
        Assert.Contains("attachment", download.Content.Headers.ContentDisposition?.ToString());
        Assert.StartsWith("%PDF", downloadContent);
        Assert.Empty(Directory.EnumerateFileSystemEntries(temporaryPath));

        var tooLarge = await client.GetAsync($"/api/trend/{limitedId}/export/pdf");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        Assert.Equal("application/problem+json", tooLarge.Content.Headers.ContentType?.MediaType);

        for (var request = 0; request < 7; request++)
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/trend/{900000 + request}/export/pdf")).StatusCode);

        var rateLimited = await client.GetAsync("/api/trend/900007/export/pdf");
        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
        if (Directory.Exists(temporaryPath)) Directory.Delete(temporaryPath, recursive: true);
    }

    private async Task<(int DownloadId, int LimitedId)> AddWaitingTimesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var download = new WaitingTime { Name = "PDF export", Hospital = Hospital.Klaksvikar, WaitValue = 3, Unit = WaitUnit.Vikur };
        var limited = new WaitingTime { Name = "Limited export", Hospital = Hospital.Klaksvikar, WaitValue = 3, Unit = WaitUnit.Vikur };
        db.WaitingTimes.AddRange(download, limited);
        await db.SaveChangesAsync();
        db.WaitingTimeHistoryEntries.AddRange(
            new WaitingTimeHistoryEntry { WaitingTimeId = limited.Id, Hospital = Hospital.Klaksvikar, Name = "Limited export", Action = HistoryAction.Updated, NewWaitValue = 2, NewUnit = WaitUnit.Vikur },
            new WaitingTimeHistoryEntry { WaitingTimeId = limited.Id, Hospital = Hospital.Klaksvikar, Name = "Limited export", Action = HistoryAction.Updated, NewWaitValue = 3, NewUnit = WaitUnit.Vikur, Timestamp = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        return (download.Id, limited.Id);
    }

}
