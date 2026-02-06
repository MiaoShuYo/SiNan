using Microsoft.EntityFrameworkCore;
using SiNan.Server.Data.Entities;

namespace SiNan.Server.Data;

public sealed class SiNanDbContext : DbContext
{
    public SiNanDbContext(DbContextOptions<SiNanDbContext> options) : base(options)
    {
    }

    public DbSet<ServiceEntity> Services => Set<ServiceEntity>();
    public DbSet<ServiceInstanceEntity> ServiceInstances => Set<ServiceInstanceEntity>();
    public DbSet<ConfigItemEntity> ConfigItems => Set<ConfigItemEntity>();
    public DbSet<ConfigHistoryEntity> ConfigHistory => Set<ConfigHistoryEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceEntity>(entity =>
        {
            entity.ToTable("services");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Namespace, e.Group, e.Name }).IsUnique();
            entity.Property(e => e.Namespace).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Group).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.MetadataJson).HasMaxLength(8192);
            entity.Property(e => e.Revision).HasDefaultValue(0L);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasMany(e => e.Instances)
                .WithOne(i => i.Service)
                .HasForeignKey(i => i.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceInstanceEntity>(entity =>
        {
            entity.ToTable("service_instances");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ServiceId);
            entity.HasIndex(e => new { e.ServiceId, e.Host, e.Port }).IsUnique();
            entity.Property(e => e.InstanceId).HasMaxLength(128);
            entity.Property(e => e.Host).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Port).IsRequired();
            entity.Property(e => e.Weight).HasDefaultValue(100);
            entity.Property(e => e.MetadataJson).HasMaxLength(8192);
            entity.Property(e => e.Healthy).HasDefaultValue(true);
            entity.Property(e => e.TtlSeconds).HasDefaultValue(30);
            entity.Property(e => e.IsEphemeral).HasDefaultValue(true);
            entity.Property(e => e.LastHeartbeatAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ConfigItemEntity>(entity =>
        {
            entity.ToTable("config_items");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Namespace, e.Group, e.Key }).IsUnique();
            entity.Property(e => e.Namespace).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Group).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Key).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(65535).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.Property(e => e.PublishedBy).HasMaxLength(128);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasMany(e => e.History)
                .WithOne(h => h.Config)
                .HasForeignKey(h => h.ConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConfigHistoryEntity>(entity =>
        {
            entity.ToTable("config_history");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ConfigId, e.Version }).IsUnique();
            entity.Property(e => e.Content).HasMaxLength(65535).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PublishedBy).HasMaxLength(128);
            entity.Property(e => e.PublishedAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.Actor).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Resource).HasMaxLength(256).IsRequired();
            entity.Property(e => e.BeforeJson).HasMaxLength(65535);
            entity.Property(e => e.AfterJson).HasMaxLength(65535);
            entity.Property(e => e.TraceId).HasMaxLength(128);
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
