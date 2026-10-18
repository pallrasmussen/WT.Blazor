using Microsoft.EntityFrameworkCore;
using WT.Blazor.Models;

namespace WT.Blazor.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<WaitingTime> WaitingTimes => Set<WaitingTime>();
    public DbSet<WaitingTimeHistoryEntry> WaitingTimeHistoryEntries => Set<WaitingTimeHistoryEntry>();
    public DbSet<PortalSettings> PortalSettings => Set<PortalSettings>();
    public DbSet<PortalSettingsAuditEntry> PortalSettingsAuditEntries => Set<PortalSettingsAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
