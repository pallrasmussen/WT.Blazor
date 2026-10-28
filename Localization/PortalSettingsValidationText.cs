using WT.Blazor.Models;

namespace WT.Blazor.Localization;

public static class PortalSettingsValidationText
{
    public static string Message(PortalSettingsError error) => error == PortalSettingsError.Guidance
        ? string.Format(AppText.Get("PatientGuidanceValidation"), FieldLimits.PatientGuidance)
        : AppText.Get(error switch
        {
            PortalSettingsError.EstimateExpiry => "EstimateExpiryValidation",
            PortalSettingsError.GreenThreshold => "GreenThresholdValidation",
            PortalSettingsError.YellowThreshold => "YellowThresholdValidation",
            _ => "LongWaitThresholdValidation"
        });
}
