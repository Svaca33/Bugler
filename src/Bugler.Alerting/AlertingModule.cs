using Bugler.Access.Contracts;
using Bugler.Alerting.ActOnEpisodes;
using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.DescribeEpisode;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.DropDeletedTargets;
using Bugler.Alerting.DropDeletedUserSubscriptions;
using Bugler.Alerting.ListEpisodes;
using Bugler.Alerting.ManageAlertingSettings;
using Bugler.Alerting.ManageSubscriptions;
using Bugler.Alerting.ReadEffectiveSensitivity;
using Bugler.Alerting.SummarizeEpisodesByService;
using Bugler.Alerting.WatchHealthChecks;
using Bugler.Alerting.WriteReadings;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        // Second bind onto the same options: PublicBaseUrl describes the server, so it is
        // configured once and read by whoever puts links in messages (ADR 0011).
        services.Configure<AlertingOptions>(configuration.GetSection(AlertingOptions.ServerSectionName));
        services.AddDbContext<AlertingDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "alerting"))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<EpisodeDetector>();
        services.AddSingleton<EpisodeCloser>();
        services.AddSingleton<DeliveryRunner>();
        // The tally of consecutive failures is the watcher's own memory, so it outlives a sweep.
        services.AddSingleton<HealthCheckWatcher>();
        services.AddHostedService<AlertingScheduler>();

        // The Readings run on their own loop: a slow model must not hold up probing or delivery
        // (Alerting ADR 0009 — the door is held by the delivery sweep, never by this beat).
        services.AddSingleton<ReadingWriter>();
        services.AddHostedService<ReadingScheduler>();

        // The typed client stays transient so the factory can rotate its handlers; the runner
        // resolves IChatSender from its per-run scope.
        services.AddHttpClient<GoogleChatSender>();
        services.AddTransient<IChatSender>(p => p.GetRequiredService<GoogleChatSender>());

        // Redirects are off by design: a health endpoint that bounces to a login page would
        // otherwise be judged alive by the login page's 200 (see CONTEXT.md: Health Check).
        services.AddHttpClient<HealthProbe>(client =>
                client.Timeout = TimeSpan.FromSeconds(HealthProbe.TimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });

        services.AddScoped<IIntegrationEventHandler<ServicesDeleted>, DeletedServicesHandler>();
        services.AddScoped<IIntegrationEventHandler<ApplicationDeleted>, DeletedApplicationHandler>();
        services.AddScoped<IIntegrationEventHandler<UserDeleted>, DeletedUserHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapAlerting(this IEndpointRouteBuilder endpoints)
    {
        // The group names the capability it needs, not the role that currently grants it (ADR 0015).
        var admin = endpoints.MapGroup("/api/admin")
            .RequireAuthorization(Capabilities.ConfigureAlerting);
        admin.MapGet("/applications/{applicationId:guid}/alerting",
            AdminAlertingEndpoints.GetApplicationAlerting);
        admin.MapPut("/applications/{applicationId:guid}/alerting",
            AdminAlertingEndpoints.SetApplicationAlerting);
        admin.MapPut("/applications/{applicationId:guid}/alerting/webhook",
            AdminAlertingEndpoints.SetChatWebhook);
        // What "the same trouble" means for this Application (ADR 0033, 0034). Admin only, like
        // the rest of alerting settings: it re-partitions every open Episode it touches.
        admin.MapPut("/applications/{applicationId:guid}/alerting/grouping",
            AdminAlertingEndpoints.SetFingerprintRule)
            .Produces<RegroupedDto>();
        admin.MapPut("/services/{serviceId:guid}/alerting",
            AdminAlertingEndpoints.SetServiceAlerting)
            .Produces<SetServiceAlertingResponse>();
        admin.MapPut("/episodes/{id:guid}/quiet-window", FingerprintQuietWindowEndpoint.Set);

        var user = endpoints.MapGroup("/api/alerting").RequireAuthorization();
        user.MapGet("/subscriptions", SubscriptionEndpoints.GetOwn).Produces<SubscriptionsDto>();
        user.MapPut("/subscriptions", SubscriptionEndpoints.SetOwn);
        user.MapGet("/episodes", EpisodesEndpoint.Handle).Produces<ListEpisodesResponse>();
        user.MapGet("/episodes/counts", EpisodeCountsEndpoint.Handle).Produces<EpisodeCountsResponse>();
        user.MapGet("/episodes/by-service", EpisodesByServiceEndpoint.Handle)
            .Produces<EpisodesByServiceResponse>();
        user.MapGet("/episodes/{id:guid}/detail", EpisodeDetailEndpoint.Handle).Produces<EpisodeDetailDto>();
        user.MapGet("/sensitivity", EffectiveSensitivityEndpoint.Handle).Produces<ListSensitivityResponse>();
        user.MapPost("/episodes/{id:guid}/acknowledge", EpisodeActionEndpoints.Acknowledge);
        user.MapDelete("/episodes/{id:guid}/acknowledgement", EpisodeActionEndpoints.Unacknowledge);
        user.MapPost("/episodes/{id:guid}/solve", EpisodeActionEndpoints.Solve);
        // The human answers to the machine hand: reject its proposal, sweep its resignation
        // aside, withdraw its claim. Confirming a proposal is the Solve above — same verdict.
        user.MapPost("/episodes/{id:guid}/proposal/reject", EpisodeActionEndpoints.RejectProposal);
        user.MapDelete("/episodes/{id:guid}/resignation", EpisodeActionEndpoints.DismissResignation);
        user.MapDelete("/episodes/{id:guid}/claim", EpisodeActionEndpoints.WithdrawClaim);

        return endpoints;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AlertingDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
