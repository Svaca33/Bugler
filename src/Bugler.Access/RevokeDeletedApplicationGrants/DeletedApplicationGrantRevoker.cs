using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bugler.Access.RevokeDeletedApplicationGrants;

/// <summary>
/// Drops the read grants pointing at an Application that no longer exists. Deleting by
/// Application is idempotent, as at-least-once delivery requires.
/// </summary>
internal sealed class DeletedApplicationGrantRevoker(
    AccessDbContext dbContext,
    ILogger<DeletedApplicationGrantRevoker> logger) : IIntegrationEventHandler<ApplicationDeleted>
{
    public async Task HandleAsync(ApplicationDeleted integrationEvent, CancellationToken cancellationToken)
    {
        var applicationId = integrationEvent.ApplicationId;
        var revoked = await dbContext.ApplicationGrants
            .Where(grant => grant.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);

        if (revoked > 0)
        {
            logger.LogInformation(
                "Revoked {Revoked} grants to deleted application {ApplicationId}", revoked, applicationId);
        }
    }
}
