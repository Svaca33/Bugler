using Bugler.Exploration.GetTraceWaterfall;
using Bugler.Exploration.ListTraces;
using Bugler.Exploration.Scoping;
using Bugler.Exploration.SearchLogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.Exploration;

/// <summary>Composition entry point of the Exploration context (the read path).</summary>
public static class ExplorationModule
{
    public static IServiceCollection AddExploration(this IServiceCollection services)
    {
        services.AddScoped<ScopeResolver>();
        return services;
    }

    public static IEndpointRouteBuilder MapExploration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/logs", SearchLogsEndpoint.Handle);
        endpoints.MapGet("/api/logs/{id:long}", SearchLogsEndpoint.HandleDetail);
        endpoints.MapGet("/api/traces", ListTracesEndpoint.Handle);
        endpoints.MapGet("/api/traces/{traceId}", GetTraceWaterfallEndpoint.Handle);
        return endpoints;
    }
}
