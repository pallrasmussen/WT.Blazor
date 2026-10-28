using WT.Blazor.Models;

namespace WT.Blazor.Localization;

public static class PortalSettingsValidationText
{
    public static string Message(PortalSettingsError error) => AppText.Get(error switch
    {
        PortalSettingsError.Guidance => "PatientGuidanceValidation",
        PortalSettingsError.EstimateExpiry => "EstimateExpiryValidation",
        PortalSettingsError.GreenThreshold => "GreenThresholdValidation",
        PortalSettingsError.YellowThreshold => "YellowThresholdValidation",
        _ => "LongWaitThresholdValidation"
    });
}
