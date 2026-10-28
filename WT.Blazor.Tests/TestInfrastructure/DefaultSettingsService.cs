using WT.Blazor.Models;
using WT.Blazor.Services;

namespace WT.Blazor.Tests.TestInfrastructure;

internal sealed class DefaultSettingsService : IPortalSettingsService
{
    public Task<PortalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PortalSettings());
    public Task SaveAsync(PortalSettings settings, string username, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<PortalSettingsAuditEntry>> GetRecentChangesAsync(int maximumEntries = 10, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
