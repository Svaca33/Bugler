using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.DropDeletedTargets;

/// <summary>
/// A deleted Application takes its alerting settings and Subscriptions with it. Registry emits
/// `ServicesDeleted` alongside, so the by-application sweeps of Episodes and overrides normally
/// hit nothing — they exist so a parked sibling event cannot leave orphans forever.
/// </summary>
internal sealed class DeletedApplicationHandler(
    AlertingDbContext dbContext, ILogger<DeletedApplicationHandler> logger)
    : IIntegrationEventHandler<ApplicationDeleted>
{
    public async Task HandleAsync(ApplicationDeleted integrationEvent, CancellationToken cancellationToken)
    {
        var applicationId = integrationEvent.ApplicationId;

        var settings = await dbContext.ApplicationSettings
            .Where(s => s.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);
        var subscriptions = await dbContext.Subscriptions
            .Where(s => s.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);
        var episodes = await dbContext.Episodes
            .Where(e => e.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);
        var overrides = await dbContext.ServiceSettings
            .Where(s => s.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);
        overrides += await dbContext.FingerprintQuietWindows
            .Where(w => w.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);

        if (settings + subscriptions + episodes + overrides > 0)
        {
            logger.LogInformation(
                "Dropped alerting state of deleted application {ApplicationId}", applicationId);
        }
    }
}
