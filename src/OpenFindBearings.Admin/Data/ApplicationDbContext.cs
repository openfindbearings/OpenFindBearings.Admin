using Microsoft.EntityFrameworkCore;
using OpenFindBearings.Admin.Models.Entities;

namespace OpenFindBearings.Admin.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; } = default!;
    public DbSet<AdminConfig> AdminConfigs { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminAuditLog>(e =>
        {
            e.ToTable("admin_audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(256);
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(128);
            e.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(128);
            e.Property(x => x.ResourceId).HasColumnName("resource_id").HasMaxLength(128);
            e.Property(x => x.Detail).HasColumnName("detail");
            e.Property(x => x.Result).HasColumnName("result").HasMaxLength(32);
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Action);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<AdminConfig>(e =>
        {
            e.ToTable("admin_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(128);
            e.Property(x => x.Value).HasColumnName("value");
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(512);
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}
