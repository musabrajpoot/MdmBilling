
using MdmBilling.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MdmBilling.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServerGroup> ServerGroups => Set<ServerGroup>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<DatabaseConnection> DatabaseConnections => Set<DatabaseConnection>();
    public DbSet<BillingBatch> BillingBatches => Set<BillingBatch>();
    public DbSet<UploadedBatchFile> UploadedBatchFiles => Set<UploadedBatchFile>();
    public DbSet<BatchMeter> BatchMeters => Set<BatchMeter>();
    public DbSet<BatchProcessingRun> BatchProcessingRuns => Set<BatchProcessingRun>();
    public DbSet<MeterServerMapping> MeterServerMappings => Set<MeterServerMapping>();
    public DbSet<MeterStatusResult> MeterStatusResults => Set<MeterStatusResult>();
    public DbSet<DailyDataAvailability> DailyDataAvailabilities => Set<DailyDataAvailability>();
    public DbSet<ServerRunStatus> ServerRunStatuses => Set<ServerRunStatus>();
    public DbSet<GeneratedReport> GeneratedReports => Set<GeneratedReport>();
    public DbSet<BatchServer> BatchServers => Set<BatchServer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ServerGroup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Server>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<BillingBatch>(e =>
        {
            e.Property(x => x.BatchNumber).HasMaxLength(50);
            e.HasIndex(x => x.BatchNumber);
        });

        b.Entity<BatchMeter>(e =>
        {
            e.Property(x => x.MeterNumber).HasMaxLength(50);
            e.HasIndex(x => new { x.BatchId, x.MeterNumber }).IsUnique();
        });

        b.Entity<ServerRunStatus>()
            .HasKey(x => new { x.RunId, x.ServerId });

        b.Entity<MeterStatusResult>()
            .HasIndex(x => new { x.RunId, x.BatchMeterId });

        b.Entity<DailyDataAvailability>()
            .HasIndex(x => new { x.RunId, x.BatchMeterId });

        b.Entity<AuditLog>()
            .HasIndex(x => x.Timestamp);

        b.Entity<BatchServer>().HasKey(x => new { x.BatchId, x.ServerId });

        // History is never deleted by cascade.
        // SQL Server also rejects multiple cascade paths.
        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == "MdmBilling.Models")
                     .SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
