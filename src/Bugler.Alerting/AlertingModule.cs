using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bugler.Alerting;

/// <summary>Composition entry point of the Alerting context (the unattended watch over incoming logs).</summary>
public static class AlertingModule
{
    public static IServiceCollection AddAlerting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AlertingOptions>(configuration.GetSection(AlertingOptions.SectionName));
        services.AddDbContext<AlertingDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "alerting"))
            .UseSnakeCaseNamingConvention());

        return services;
    }

    public static IEndpointRouteBuilder MapAlerting(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AlertingDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
