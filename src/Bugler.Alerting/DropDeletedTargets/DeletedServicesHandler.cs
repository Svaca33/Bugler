using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.DropDeletedTargets;

/// <summary>
/// Deleted Services take their alerting traces with them: Episodes (Deliveries follow by
/// cascade), overrides, and the Subscriptions pointing at them. Idempotent — a re-delivered
/// event deletes nothing twice.
/// </summary>
internal sealed class DeletedServicesHandler(
    AlertingDbContext dbContext, ILogger<DeletedServicesHandler> logger)
    : IIntegrationEventHandler<ServicesDeleted>
{
    public async Task HandleAsync(ServicesDeleted integrationEvent, CancellationToken cancellationToken)
    {
        var serviceIds = integrationEvent.ServiceIds;

        var episodes = await dbContext.Episodes
            .Where(e => serviceIds.Contains(e.ServiceId))
            .ExecuteDeleteAsync(cancellationToken);
        var settings = await dbContext.ServiceSettings
            .Where(s => serviceIds.Contains(s.ServiceId))
            .ExecuteDeleteAsync(cancellationToken);
        settings += await dbContext.FingerprintQuietWindows
            .Where(w => serviceIds.Contains(w.ServiceId))
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
