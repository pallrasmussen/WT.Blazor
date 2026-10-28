using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WT.Blazor.Models;

namespace WT.Blazor.Data.Configurations;

public sealed class PortalSettingsAuditEntryConfiguration : IEntityTypeConfiguration<PortalSettingsAuditEntry>
{
    public void Configure(EntityTypeBuilder<PortalSettingsAuditEntry> builder)
    {
        builder.Property(entry => entry.Username).HasMaxLength(FieldLimits.AuditUsername);
        builder.Property(entry => entry.OldReferralInstructionsFo).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.OldReferralInstructionsEn).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.OldContactGuidanceFo).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.OldContactGuidanceEn).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.NewReferralInstructionsFo).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.NewReferralInstructionsEn).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.NewContactGuidanceFo).HasMaxLength(FieldLimits.PatientGuidance);
        builder.Property(entry => entry.NewContactGuidanceEn).HasMaxLength(FieldLimits.PatientGuidance);
        builder.HasIndex(entry => entry.Timestamp);
    }
}