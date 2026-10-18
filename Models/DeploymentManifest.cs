using System.Text.Json;

namespace WT.Blazor.Models;

public sealed record DeploymentManifest(string BuildVersion, string CommitSha, DateTimeOffset PublishedAtUtc)
{
    public static DeploymentManifest Load(string path)
    {
        if (!File.Exists(path)) return Unknown;

        try
        {
            return JsonSerializer.Deserialize<DeploymentManifest>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? Unknown;
        }
        catch (JsonException)
        {
            return Unknown;
        }
    }

    public static DeploymentManifest Unknown { get; } = new("unknown", "unknown", DateTimeOffset.MinValue);
}