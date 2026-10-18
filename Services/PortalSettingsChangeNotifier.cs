namespace WT.Blazor.Services;

public sealed class PortalSettingsChangeNotifier
{
    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}