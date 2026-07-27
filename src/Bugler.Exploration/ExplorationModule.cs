using Bugler.Exploration.BrowseCatalog;
using Bugler.Exploration.GetTraceWaterfall;
using Bugler.Exploration.ListTraces;
using Bugler.Exploration.Scoping;
using Bugler.Exploration.SearchLogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        var group = endpoints.MapGroup("").RequireAuthorization();
        group.MapGet("/api/logs", SearchLogsEndpoint.Handle).Produces<SearchLogsResponse>();
        group.MapGet("/api/logs/{id:long}", SearchLogsEndpoint.HandleDetail).Produces<LogRecordDto>();
        group.MapGet("/api/traces", ListTracesEndpoint.Handle).Produces<ListTracesResponse>();
        group.MapGet("/api/traces/{traceId}", GetTraceWaterfallEndpoint.Handle).Produces<TraceDetailResponse>();
        group.MapGet("/api/catalog", CatalogEndpoint.Handle);
        return endpoints;
    }
}
