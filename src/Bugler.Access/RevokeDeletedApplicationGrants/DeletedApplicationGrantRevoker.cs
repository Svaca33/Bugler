using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bugler.Access.RevokeDeletedApplicationGrants;

/// <summary>
/// Drops the read grants — and the Focus rows — pointing at an Application that no longer exists.
/// Deleting by Application is idempotent, as at-least-once delivery requires.
///
/// The two go together because they are the same shape of dangling reference: a grant to nothing
/// is a permission nobody can use, and a Focus on nothing is attention nobody can spend.
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

        var unfocused = await dbContext.ApplicationFocuses
            .Where(focus => focus.ApplicationId == applicationId)
            .ExecuteDeleteAsync(cancellationToken);

        if (revoked > 0 || unfocused > 0)
        {
            logger.LogInformation(
                "Revoked {Revoked} grants and {Unfocused} focus entries to deleted application {ApplicationId}",
                revoked, unfocused, applicationId);
        }
    }
}
