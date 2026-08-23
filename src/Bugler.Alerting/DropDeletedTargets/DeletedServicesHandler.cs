using Bugler.Alerting.Settings;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.DropDeletedTargets;

/// <summary>
/// Deleted Services take their alerting traces with them: their Participations, their overrides,
/// and the Subscriptions pointing at them. An Episode no longer goes with them, because it is no
/// longer theirs (ADR 0034) — it falls only when its last Participation does, that is, only when
/// nobody may still see anything in it. Idempotent — a re-delivered event deletes nothing twice.
/// </summary>
internal sealed class DeletedServicesHandler(
    AlertingDbContext dbContext, ILogger<DeletedServicesHandler> logger)
    : IIntegrationEventHandler<ServicesDeleted>
{
    public async Task HandleAsync(ServicesDeleted integrationEvent, CancellationToken cancellationToken)
    {
        var serviceIds = integrationEvent.ServiceIds;

        await dbContext.Participations
            .Where(p => serviceIds.Contains(p.ServiceId))
            .ExecuteDeleteAsync(cancellationToken);
        // The opening evidence loses its name; the Episode stands as long as anyone is in it.
        await dbContext.Episodes
            .Where(e => e.OpenedByServiceId != null && serviceIds.Contains(e.OpenedByServiceId.Value))
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.OpenedByServiceId, (ServiceId?)null),
                cancellationToken);
        var episodes = await dbContext.Episodes
            .Where(e => !dbContext.Participations.Any(p => p.EpisodeId == e.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var settings = await dbContext.ServiceSettings
            .Where(s => serviceIds.Contains(s.ServiceId))
            .ExecuteDeleteAsync(cancellationToken);
        // Quiet Window overrides are the Scope's now, and a Scope outlives any one of its
        // Services — except the Scope that *is* one Service, which the Health Check Watch uses.
        var ownScopeKeys = serviceIds.Select(EpisodeScope.KeyOfService).ToList();
        settings += await dbContext.FingerprintQuietWindows
            .Where(w => ownScopeKeys.Contains(w.ScopeKey))
            .ExecuteDeleteAsync(cancellationToken);
        var subscriptions = await dbContext.Subscriptions
            .Where(s => s.ServiceId != null && serviceIds.Contains(s.ServiceId!.Value))
            .ExecuteDeleteAsync(cancellationToken);

        if (episodes + settings + subscriptions > 0)
        {
            logger.LogInformation(
                "Dropped alerting state of {Count} deleted services: {Episodes} episodes, "
                + "{Settings} overrides, {Subscriptions} subscriptions",
                serviceIds.Count, episodes, settings, subscriptions);
        }
    }
}
