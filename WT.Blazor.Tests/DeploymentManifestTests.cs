using WT.Blazor.Models;
using Xunit;

namespace WT.Blazor.Tests;

public class DeploymentManifestTests
{
    [Fact]
    public void LoadReadsPublishedManifest()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"buildVersion\":\"1.2.3\",\"commitSha\":\"abc123\",\"publishedAtUtc\":\"2026-09-27T12:00:00Z\"}");

            var manifest = DeploymentManifest.Load(path);

            Assert.Equal("1.2.3", manifest.BuildVersion);
            Assert.Equal("abc123", manifest.CommitSha);
            Assert.Equal(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero), manifest.PublishedAtUtc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadReturnsUnknownForInvalidManifest()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not json");

            Assert.Same(DeploymentManifest.Unknown, DeploymentManifest.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}