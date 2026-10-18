namespace WT.Blazor.Services;

public sealed class PortalSettingsConcurrencyException : Exception
{
    public PortalSettingsConcurrencyException()
        : base("Portal settings were changed by another administrator. Reload the page before saving again.")
    {
    }

    public PortalSettingsConcurrencyException(Exception innerException)
        : base("Portal settings were changed by another administrator. Reload the page before saving again.", innerException)
    {
    }
}
