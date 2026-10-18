namespace WT.Blazor.Models;

public sealed class AdGroupsOptions
{
    public string Viewer { get; set; } = string.Empty;
    public string Contributor { get; set; } = string.Empty;
    // Backward-compatible alias for deployed configuration files using the original typo.
    public string Contributer { get; set; } = string.Empty;
    public string Admin { get; set; } = string.Empty;

    public void ApplyDefaults(string machineName)
    {
        Viewer = string.IsNullOrWhiteSpace(Viewer) ? $@"{machineName}\WT-Viewers" : Viewer;
        Contributor = string.IsNullOrWhiteSpace(Contributor) ? Contributer : Contributor;
        Contributor = string.IsNullOrWhiteSpace(Contributor) ? $@"{machineName}\WT-Contributors" : Contributor;
        Admin = string.IsNullOrWhiteSpace(Admin) ? $@"{machineName}\WT-Admins" : Admin;
    }
}