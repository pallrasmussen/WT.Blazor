using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WT.Blazor.Models;

namespace WT.Blazor.Data.Configurations;

public sealed class PortalSettingsConfiguration : IEntityTypeConfiguration<PortalSettings>
{
    public void Configure(EntityTypeBuilder<PortalSettings> builder)
    {
        builder.Property(settings => settings.ReferralInstructionsFo).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(settings => settings.ReferralInstructionsEn).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(settings => settings.ContactGuidanceFo).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(settings => settings.ContactGuidanceEn).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(settings => settings.EstimateExpiryMonths).HasDefaultValue(12);
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Version).IsConcurrencyToken();
    }
}