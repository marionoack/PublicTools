using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OneTimeShare.Models;

namespace OneTimeShare.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SharedAsset> SharedAssets => Set<SharedAsset>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SharedAsset>(e =>
        {
            e.HasIndex(a => a.PublicToken).IsUnique();
            e.HasOne(a => a.Owner)
             .WithMany()
             .HasForeignKey(a => a.OwnerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.EventType);
        });
    }
}
