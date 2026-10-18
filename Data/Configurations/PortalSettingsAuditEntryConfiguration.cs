using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WT.Blazor.Models;

namespace WT.Blazor.Data.Configurations;

public sealed class PortalSettingsAuditEntryConfiguration : IEntityTypeConfiguration<PortalSettingsAuditEntry>
{
    public void Configure(EntityTypeBuilder<PortalSettingsAuditEntry> builder)
    {
        builder.Property(entry => entry.Username).HasMaxLength(256);
        builder.HasIndex(entry => entry.Timestamp);
    }
}