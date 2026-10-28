namespace WT.Blazor.Models;

public enum PortalSettingsError { Guidance, EstimateExpiry, GreenThreshold, YellowThreshold, LongWaitThreshold }

public static class PortalSettingsValidation
{
    public static void Normalize(PortalSettings settings)
    {
        settings.ReferralInstructionsFo = TrimOptional(settings.ReferralInstructionsFo);
        settings.ReferralInstructionsEn = TrimOptional(settings.ReferralInstructionsEn);
        settings.ContactGuidanceFo = TrimOptional(settings.ContactGuidanceFo);
        settings.ContactGuidanceEn = TrimOptional(settings.ContactGuidanceEn);
    }

    public static PortalSettingsError? Validate(PortalSettings settings)
    {
        var guidance = new[] { settings.ReferralInstructionsFo, settings.ReferralInstructionsEn, settings.ContactGuidanceFo, settings.ContactGuidanceEn };
        if (guidance.Any(text => text?.Length > 2000) || settings.PublishPatientGuidance && guidance.Any(string.IsNullOrWhiteSpace))
            return PortalSettingsError.Guidance;
        if (settings.EstimateExpiryMonths is < 1 or > 60) return PortalSettingsError.EstimateExpiry;
        if (settings.GreenMaximumAgeMonths is < 1 or > 24) return PortalSettingsError.GreenThreshold;
        if (settings.YellowMaximumAgeMonths is < 2 or > 36 || settings.YellowMaximumAgeMonths <= settings.GreenMaximumAgeMonths)
            return PortalSettingsError.YellowThreshold;
        if (settings.LongWaitMonths is < 1 or > 60) return PortalSettingsError.LongWaitThreshold;
        return null;
    }

    public static void ThrowIfInvalid(PortalSettings settings)
    {
        if (Validate(settings) is not { } error) return;
        throw error switch
        {
            PortalSettingsError.Guidance => new ArgumentException("Both language versions of published guidance are required and each field must be at most 2000 characters."),
            PortalSettingsError.EstimateExpiry => new ArgumentOutOfRangeException(nameof(settings.EstimateExpiryMonths)),
            PortalSettingsError.GreenThreshold => new ArgumentOutOfRangeException(nameof(settings.GreenMaximumAgeMonths)),
            PortalSettingsError.YellowThreshold => new ArgumentOutOfRangeException(nameof(settings.YellowMaximumAgeMonths)),
            _ => new ArgumentOutOfRangeException(nameof(settings.LongWaitMonths))
        };
    }

    private static string? TrimOptional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
