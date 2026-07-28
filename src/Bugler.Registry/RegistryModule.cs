using Bugler.Registry.ApiKeyValidation;
using Bugler.Registry.BrowseCatalog;
using Bugler.Registry.Contracts;
using Bugler.Registry.ManageApiKeys;
using Bugler.Registry.ManageCatalog;
using Bugler.Registry.RetentionPolicy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bugler.Registry;

/// <summary>Composition entry point of the Registry context (telemetry topology).</summary>
public static class RegistryModule
{
    public static IServiceCollection AddRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RegistryOptions>(configuration.GetSection(RegistryOptions.SectionName));
        services.AddDbContext<RegistryDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "registry"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddScoped<IRetentionReader, RetentionReader>();
        return services;
    }

    public static IEndpointRouteBuilder MapRegistry(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization("Admin");
        admin.MapGet("/applications", AdminCatalogEndpoints.ListApplications);
        admin.MapPost("/applications", AdminCatalogEndpoints.CreateApplication)
            .Produces<ApplicationDto>();
        admin.MapGet("/applications/{applicationId:guid}/services", AdminCatalogEndpoints.ListServices);
        admin.MapPost("/services", AdminCatalogEndpoints.CreateService).Produces<ServiceDto>();
        admin.MapPut("/services/{id:guid}/retention", AdminCatalogEndpoints.SetRetention);
        admin.MapGet("/services/{id:guid}/keys", AdminApiKeyEndpoints.ListKeys)
            .Produces<List<ApiKeyDto>>();
        admin.MapPost("/services/{id:guid}/keys", AdminApiKeyEndpoints.IssueKey)
            .Produces<IssuedApiKeyDto>();
        admin.MapDelete("/keys/{id:guid}", AdminApiKeyEndpoints.RevokeKey);
        return endpoints;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RegistryDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
