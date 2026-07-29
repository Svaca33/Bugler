using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.CloseQuietEpisodes;

/// <summary>
/// One closing sweep: an open Episode whose Service stayed quiet for its Quiet Window closes
/// with an All Clear owed to exactly the recipients of its Alert; one whose Sensitivity turned
/// Off closes silently (the net for a detect run racing a settings change). One transaction.
/// </summary>
public sealed class EpisodeCloser(
    IServiceScopeFactory scopeFactory,
    ILogger<EpisodeCloser> logger)
{
    public async Task CloseOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();

        var openEpisodes = await dbContext.Episodes
            .Where(e => e.ClosedAt == null)
            .ToListAsync(cancellationToken);
        if (openEpisodes.Count == 0)
        {
            return;
        }

        var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
            .GetServicesAsync(cancellationToken);
        var effective = EffectiveSettings.Build(
            catalog,
            await dbContext.ApplicationSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.ServiceSettings.AsNoTracking().ToListAsync(cancellationToken));

        var now = DateTimeOffset.UtcNow;
        var mutedServices = new List<ServiceId>();

        foreach (var episode in openEpisodes)
        {
            var reason = CloseDecision.Decide(
                effective.SensitivityOf(episode.ServiceId),
                episode.LastMatchAt,
                effective.QuietWindowOf(episode.ServiceId),
                now);

            switch (reason)
            {
                case EpisodeCloseReason.SensitivityOff:
                    mutedServices.Add(episode.ServiceId); // SilentClose closes and lapses below.
                    break;

                case EpisodeCloseReason.QuietWindow:
                    episode.ClosedAt = now;
                    episode.CloseReason = EpisodeCloseReason.QuietWindow;
                    await EnqueueAllClearsAsync(dbContext, episode, now, cancellationToken);
                    logger.LogInformation(
                        "Episode of service {ServiceId} fell quiet after {Errors} errors "
                        + "and {Warns} warnings",
                        episode.ServiceId, episode.ErrorCount, episode.WarnCount);
                    break;
            }
        }

        await SilentClose.ApplyAsync(dbContext, mutedServices, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The All Clear goes to exactly who got the Alert: to a mid-episode subscriber it would
    /// announce a resolution of nothing, and a mid-episode unsubscriber still deserves the end
    /// of the story they were told started. Access is still re-checked at send time.
    /// </summary>
    private static async Task EnqueueAllClearsAsync(
        AlertingDbContext dbContext, Episode episode, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var alerts = await dbContext.Deliveries
            .AsNoTracking()
            .Where(d => d.EpisodeId == episode.Id && d.Kind == DeliveryKind.Alert)
            .ToListAsync(cancellationToken);

        foreach (var alert in alerts)
        {
            dbContext.Deliveries.Add(new Delivery
            {
                Id = Guid.CreateVersion7(),
                EpisodeId = episode.Id,
                Kind = DeliveryKind.AllClear,
                Channel = alert.Channel,
                UserId = alert.UserId,
                CreatedAt = now,
                NextAttemptAt = now,
            });
        }
    }
}
