using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bugler.Ingestion.Storage;

/// <summary>Design-time factory for `dotnet ef migrations` — never used at runtime.</summary>
internal sealed class IngestionDbContextFactory : IDesignTimeDbContextFactory<IngestionDbContext>
{
    public IngestionDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IngestionDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=bugler",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "telemetry"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
