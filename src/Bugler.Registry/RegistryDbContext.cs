using Bugler.Registry.Catalog;
using Bugler.Registry.Outbox;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options)
    : DbContext(options)
{
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("registry");

        modelBuilder.Entity<Application>(application =>
        {
            application.Property(a => a.Id)
                .HasConversion(id => id.Value, value => new ApplicationId(value));
            application.Property(a => a.Name).HasMaxLength(200);
            application.HasIndex(a => a.Name).IsUnique();
        });

        modelBuilder.Entity<Service>(service =>
        {
            service.Property(s => s.Id)
                .HasConversion(id => id.Value, value => new ServiceId(value));
            service.Property(s => s.ApplicationId)
                .HasConversion(id => id.Value, value => new ApplicationId(value));
            service.Property(s => s.Namespace).HasMaxLength(200);
            service.Property(s => s.Environment).HasMaxLength(200);
            service.Property(s => s.Name).HasMaxLength(200);
            service.HasIndex(s => new { s.ApplicationId, s.Namespace, s.Environment, s.Name }).IsUnique();
            service.HasOne<Application>().WithMany()
                .HasForeignKey(s => s.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(apiKey =>
        {
            apiKey.Property(k => k.ServiceId)
                .HasConversion(id => id.Value, value => new ServiceId(value));
            apiKey.HasIndex(k => k.KeyHash).IsUnique();
            apiKey.HasOne<Service>().WithMany()
                .HasForeignKey(k => k.ServiceId).OnDelete(DeleteBehavior.Cascade);
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
