using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.DropDeletedTargets;
using Bugler.Alerting.DropDeletedUserSubscriptions;
using Bugler.Alerting.ManageAlertingSettings;
using Bugler.Alerting.ManageSubscriptions;
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
        services.AddDbContext<AlertingDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "alerting"))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<EpisodeDetector>();
        services.AddSingleton<EpisodeCloser>();
        services.AddSingleton<DeliveryRunner>();
        services.AddHostedService<AlertingScheduler>();

        services.AddSingleton<IMailSender, MailKitMailSender>();
        // The typed client stays transient so the factory can rotate its handlers; the runner
        // resolves IChatSender from its per-run scope.
        services.AddHttpClient<GoogleChatSender>();
        services.AddTransient<IChatSender>(p => p.GetRequiredService<GoogleChatSender>());

        services.AddScoped<IIntegrationEventHandler<ServicesDeleted>, DeletedServicesHandler>();
        services.AddScoped<IIntegrationEventHandler<ApplicationDeleted>, DeletedApplicationHandler>();
        services.AddScoped<IIntegrationEventHandler<UserDeleted>, DeletedUserHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapAlerting(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization("Admin");
        admin.MapGet("/applications/{applicationId:guid}/alerting",
            AdminAlertingEndpoints.GetApplicationAlerting);
        admin.MapPut("/applications/{applicationId:guid}/alerting",
            AdminAlertingEndpoints.SetApplicationAlerting);
        admin.MapPut("/applications/{applicationId:guid}/alerting/webhook",
            AdminAlertingEndpoints.SetChatWebhook);
        admin.MapPut("/services/{serviceId:guid}/alerting",
            AdminAlertingEndpoints.SetServiceAlerting);

        var user = endpoints.MapGroup("/api/alerting").RequireAuthorization();
        user.MapGet("/subscriptions", SubscriptionEndpoints.GetOwn).Produces<SubscriptionsDto>();
        user.MapPut("/subscriptions", SubscriptionEndpoints.SetOwn);

        return endpoints;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AlertingDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
