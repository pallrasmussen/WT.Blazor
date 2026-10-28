using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WT.Blazor.Models;

namespace WT.Blazor.Data.Configurations;

public sealed class WaitingTimeHistoryEntryConfiguration : IEntityTypeConfiguration<WaitingTimeHistoryEntry>
{
    public void Configure(EntityTypeBuilder<WaitingTimeHistoryEntry> builder)
    {
        builder.Property(entry => entry.EstimateSeries).HasDefaultValue(1);
        builder.Property(entry => entry.ResponsibleDepartment).HasMaxLength(FieldLimits.ResponsibleDepartment);
        builder.Property(entry => entry.ComparisonCode).HasMaxLength(FieldLimits.ComparisonCode);
        builder.HasOne(entry => entry.WaitingTime)
            .WithMany(waitingTime => waitingTime.History)
            .HasForeignKey(entry => entry.WaitingTimeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entry => entry.Timestamp);
        builder.HasIndex(entry => new { entry.Hospital, entry.Department, entry.Timestamp });
        builder.HasIndex(entry => new { entry.Action, entry.Timestamp });
        builder.HasIndex(entry => new { entry.WaitingTimeId, entry.Action, entry.Timestamp });
        builder.HasIndex(entry => new { entry.Username, entry.Timestamp });
    }
}