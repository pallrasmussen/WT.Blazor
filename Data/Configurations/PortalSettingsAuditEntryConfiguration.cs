using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WT.Blazor.Models;

namespace WT.Blazor.Data.Configurations;

public sealed class PortalSettingsAuditEntryConfiguration : IEntityTypeConfiguration<PortalSettingsAuditEntry>
{
    public void Configure(EntityTypeBuilder<PortalSettingsAuditEntry> builder)
    {
        builder.Property(entry => entry.Username).HasMaxLength(256);
        builder.Property(entry => entry.OldReferralInstructionsFo).HasMaxLength(2000);
        builder.Property(entry => entry.OldReferralInstructionsEn).HasMaxLength(2000);
        builder.Property(entry => entry.OldContactGuidanceFo).HasMaxLength(2000);
        builder.Property(entry => entry.OldContactGuidanceEn).HasMaxLength(2000);
        builder.Property(entry => entry.NewReferralInstructionsFo).HasMaxLength(2000);
        builder.Property(entry => entry.NewReferralInstructionsEn).HasMaxLength(2000);
        builder.Property(entry => entry.NewContactGuidanceFo).HasMaxLength(2000);
        builder.Property(entry => entry.NewContactGuidanceEn).HasMaxLength(2000);
        builder.HasIndex(entry => entry.Timestamp);
    }
}