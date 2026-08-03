using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.Host;

/// <summary>
/// The Host's own little store: settings of the deployment itself, owned by no bounded context —
/// the composition root owns deployment topology, and a mail relay is exactly that (ADR 0014).
/// </summary>
public sealed class ServerDbContext(DbContextOptions<ServerDbContext> options)
    : DbContext(options)
{
    public DbSet<StoredSmtpSettings> SmtpSettings => Set<StoredSmtpSettings>();
    public DbSet<StoredServerLanguage> ServerLanguage => Set<StoredServerLanguage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("server");

        modelBuilder.Entity<StoredServerLanguage>(language =>
        {
            language.Property(l => l.Id).ValueGeneratedNever();
            language.Property(l => l.Language).HasMaxLength(20);
        });

        modelBuilder.Entity<StoredSmtpSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.Host).HasMaxLength(320);
            // Stored by name, not number: an operator reading the table should not need the enum.
            settings.Property(s => s.Security).HasConversion<string>().HasMaxLength(20);
            settings.Property(s => s.Username).HasMaxLength(320);
            settings.Property(s => s.Password).HasMaxLength(1000);
            settings.Property(s => s.From).HasMaxLength(320);
        });
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
