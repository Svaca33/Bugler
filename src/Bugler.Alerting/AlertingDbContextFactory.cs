using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bugler.Alerting;

/// <summary>Design-time factory for `dotnet ef migrations` — never used at runtime.</summary>
internal sealed class AlertingDbContextFactory : IDesignTimeDbContextFactory<AlertingDbContext>
{
    public AlertingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AlertingDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=bugler",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "alerting"))
            .UseSnakeCaseNamingConvention()
            .Options);
}
