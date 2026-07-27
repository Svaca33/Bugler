using Bugler.Registry.Catalog;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options)
    : DbContext(options)
{
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Instance> Instances => Set<Instance>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

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

        modelBuilder.Entity<Instance>(instance =>
        {
            instance.Property(i => i.Id)
                .HasConversion(id => id.Value, value => new InstanceId(value));
            instance.Property(i => i.ApplicationId)
                .HasConversion(id => id.Value, value => new ApplicationId(value));
            instance.Property(i => i.Name).HasMaxLength(200);
            instance.HasIndex(i => new { i.ApplicationId, i.Name }).IsUnique();
            instance.HasOne<Application>().WithMany()
                .HasForeignKey(i => i.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(apiKey =>
        {
            apiKey.Property(k => k.InstanceId)
                .HasConversion(id => id.Value, value => new InstanceId(value));
            apiKey.HasIndex(k => k.KeyHash).IsUnique();
            apiKey.HasOne<Instance>().WithMany()
                .HasForeignKey(k => k.InstanceId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
