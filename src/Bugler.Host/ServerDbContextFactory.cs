using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bugler.Host;

/// <summary>Design-time factory for `dotnet ef migrations` — never used at runtime.</summary>
internal sealed class ServerDbContextFactory : IDesignTimeDbContextFactory<ServerDbContext>
{
    public ServerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=bugler",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "server"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
