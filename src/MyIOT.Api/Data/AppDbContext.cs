using Microsoft.EntityFrameworkCore;
using MyIOT.Api.Models;
using MyIOT.Shared.Models;

namespace MyIOT.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceAttribute> DeviceAttributes => Set<DeviceAttribute>();
    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Device ──
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(d => d.Name).IsRequired().HasMaxLength(256);
            entity.Property(d => d.AccessToken).IsRequired().HasMaxLength(128);
            entity.Property(d => d.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");

            entity.HasIndex(d => d.AccessToken).IsUnique();
        });

        // ── DeviceAttribute ──
        modelBuilder.Entity<DeviceAttribute>(entity =>
        {
            entity.ToTable("device_attributes");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(a => a.Key).IsRequired().HasMaxLength(256);
            entity.Property(a => a.Value).IsRequired().HasColumnType("jsonb");
            entity.Property(a => a.Scope)
                  .IsRequired()
                  .HasConversion<string>()
                  .HasMaxLength(16);
            entity.Property(a => a.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");

            // Unique constraint: one key per scope per device
            entity.HasIndex(a => new { a.DeviceId, a.Key, a.Scope }).IsUnique();

            entity.HasOne(a => a.Device)
                  .WithMany(d => d.Attributes)
                  .HasForeignKey(a => a.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TelemetryRecord ──
        modelBuilder.Entity<TelemetryRecord>(entity =>
        {
            entity.ToTable("telemetry");
            entity.HasKey(t => new { t.DeviceId, t.Key, t.Timestamp });
            entity.Property(t => t.Key).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Timestamp).IsRequired();
            entity.Property(t => t.Value).IsRequired();

            entity.HasIndex(t => new { t.DeviceId, t.Timestamp });

            entity.HasOne(t => t.Device)
                  .WithMany(d => d.TelemetryRecords)
                  .HasForeignKey(t => t.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
