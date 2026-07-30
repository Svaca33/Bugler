using Bugler.Access.Outbox;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Access;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ApplicationGrant> ApplicationGrants => Set<ApplicationGrant>();
    public DbSet<ResetTicket> ResetTickets => Set<ResetTicket>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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

        modelBuilder.Entity<ResetTicket>(ticket =>
        {
            // Redemption knows nothing but the fingerprint it was handed; unique, because two
            // tickets sharing one would mean the random secret was not random after all.
            ticket.HasIndex(t => t.Fingerprint).IsUnique();
            ticket.HasOne<User>().WithMany()
                .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.Property(m => m.EventType).HasMaxLength(200);
            message.Property(m => m.LastError).HasMaxLength(2000);
            // The dispatcher's only query: unparked messages that have come due.
            message.HasIndex(m => new { m.ParkedAt, m.NextAttemptAt });
        });
    }
}
