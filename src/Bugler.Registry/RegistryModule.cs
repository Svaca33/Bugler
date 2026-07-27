using Bugler.Registry.ApiKeyValidation;
using Bugler.Registry.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bugler.Registry;

/// <summary>Composition entry point of the Registry context (telemetry topology).</summary>
public static class RegistryModule
{
    public static IServiceCollection AddRegistry(this IServiceCollection services)
    {
        services.AddDbContext<RegistryDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "registry"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
        return services;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RegistryDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
