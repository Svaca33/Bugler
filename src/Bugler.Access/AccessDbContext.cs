using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ApplicationGrant> ApplicationGrants => Set<ApplicationGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("access");

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).HasMaxLength(320);
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.DisplayName).HasMaxLength(200);
        });

        modelBuilder.Entity<ApplicationGrant>(grant =>
        {
            grant.Property(g => g.ApplicationId)
                .HasConversion(id => id.Value, value => new ApplicationId(value));
            grant.HasIndex(g => new { g.UserId, g.ApplicationId }).IsUnique();
            grant.HasOne<User>().WithMany()
                .HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
