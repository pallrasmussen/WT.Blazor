using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WT.Blazor.Models;

namespace WT.Blazor.Data.Configurations;

public sealed class WaitingTimeConfiguration : IEntityTypeConfiguration<WaitingTime>
{
    public void Configure(EntityTypeBuilder<WaitingTime> builder)
    {
        builder.Property(waitingTime => waitingTime.EstimateSeries).HasDefaultValue(1);
        builder.HasIndex(waitingTime => new { waitingTime.IsActive, waitingTime.Hospital, waitingTime.Department });
        builder.Property(waitingTime => waitingTime.Version).IsConcurrencyToken();
    }
}