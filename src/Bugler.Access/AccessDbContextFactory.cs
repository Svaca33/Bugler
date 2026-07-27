using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bugler.Access;

/// <summary>Design-time factory for `dotnet ef migrations` — never used at runtime.</summary>
internal sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AccessDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=bugler",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "access"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
