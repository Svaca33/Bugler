using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.DropDeletedUserSubscriptions;

/// <summary>
/// A Subscription dies with its User (CONTEXT.md), and a mail still owed to a deleted User is
/// owed to nobody — it lapses rather than waits out its TTL. Idempotent.
/// </summary>
internal sealed class DeletedUserHandler(
    AlertingDbContext dbContext, ILogger<DeletedUserHandler> logger)
    : IIntegrationEventHandler<UserDeleted>
{
    public async Task HandleAsync(UserDeleted integrationEvent, CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.Subscriptions
            .Where(s => s.UserId == integrationEvent.UserId)
            .ExecuteDeleteAsync(cancellationToken);

        var lapsedAt = DateTimeOffset.UtcNow;
        var lapsed = await dbContext.Deliveries
            .Where(d => d.UserId == integrationEvent.UserId
                && d.DeliveredAt == null && d.LapsedAt == null)
            .ExecuteUpdateAsync(
                d => d.SetProperty(x => x.LapsedAt, lapsedAt),
                cancellationToken);

        if (subscriptions + lapsed > 0)
        {
            logger.LogInformation(
                "Deleted user {UserId}: dropped {Subscriptions} subscriptions, lapsed {Lapsed} deliveries",
                integrationEvent.UserId, subscriptions, lapsed);
        }
    }
}
