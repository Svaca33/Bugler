using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bugler.Registry;

/// <summary>Design-time factory for `dotnet ef migrations` — never used at runtime.</summary>
internal sealed class RegistryDbContextFactory : IDesignTimeDbContextFactory<RegistryDbContext>
{
    public RegistryDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RegistryDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=bugler",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "registry"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
