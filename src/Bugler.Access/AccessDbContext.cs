using Bugler.Access.Outbox;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bugler.Access;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options)
    : DbContext(options)
{
    // A converter instance rather than a lambda: it applies to the nullable property as-is.
    private static readonly ValueConverter<Language, string> LanguageConverter =
        new(language => language.Code, code => new Language(code));

    /// <summary>The same trick for a Machine Delegation's narrowing, which is an Application or nothing.</summary>
    private static readonly ValueConverter<ApplicationId, Guid> ApplicationIdConverter =
        new(id => id.Value, value => new ApplicationId(value));

    public DbSet<User> Users => Set<User>();
    public DbSet<ApplicationGrant> ApplicationGrants => Set<ApplicationGrant>();
    public DbSet<ResetTicket> ResetTickets => Set<ResetTicket>();
    public DbSet<MachineDelegation> MachineDelegations => Set<MachineDelegation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("access");

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).HasMaxLength(320);
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.DisplayName).HasMaxLength(200);
            user.Property(u => u.Language)
                .HasConversion(LanguageConverter)
                .HasMaxLength(20);
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

        modelBuilder.Entity<MachineDelegation>(delegation =>
        {
            delegation.Property(d => d.Name).HasMaxLength(100);
            delegation.Property(d => d.ApplicationId).HasConversion(ApplicationIdConverter);
            // The only lookup on the authentication path: a presented Secret, hashed, once per
            // request. Unique for the reason a Reset Ticket's fingerprint is.
            delegation.HasIndex(d => d.Fingerprint).IsUnique();
            delegation.HasOne<User>().WithMany()
                .HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
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
