using Bugler.Ingestion.EraseDeletedServiceTelemetry;
using Bugler.Ingestion.ObserveReleases;
using Bugler.Ingestion.PurgeExpiredTelemetry;
using Bugler.Ingestion.ReceiveOtlpLogs;
using Bugler.Ingestion.ReceiveOtlpTraces;
using Bugler.Ingestion.Storage;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bugler.Ingestion;

/// <summary>Composition entry point of the Ingestion context (the write path).</summary>
public static class IngestionModule
{
    public static IServiceCollection AddIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGrpc();
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));
        services.AddSingleton<TelemetryBuffer>();
        services.AddHostedService<LogWriter>();
        services.AddHostedService<SpanWriter>();
        services.AddSingleton<ReleaseObservations>();
        services.AddHostedService<ReleaseRecorder>();
        services.AddSingleton<TelemetryPurger>();
        services.AddHostedService<PurgeScheduler>();
        services.AddScoped<IIntegrationEventHandler<ServicesDeleted>, DeletedServiceEraser>();

        services.AddDbContext<IngestionDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "telemetry"))
            .UseSnakeCaseNamingConvention());

        return services;
    }

    /// <summary>OTLP/gRPC receivers (the ExportService pair).</summary>
    public static IEndpointRouteBuilder MapOtlpGrpcIngestion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<OtlpLogsGrpcService>();
        endpoints.MapGrpcService<OtlpTraceGrpcService>();
        return endpoints;
    }

    /// <summary>OTLP/HTTP receivers — protobuf POST /v1/logs and /v1/traces.</summary>
    public static IEndpointRouteBuilder MapOtlpHttpIngestion(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/logs", IngestLogsHttpEndpoint.Handle);
        endpoints.MapPost("/v1/traces", IngestTracesHttpEndpoint.Handle);
        return endpoints;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestionDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
