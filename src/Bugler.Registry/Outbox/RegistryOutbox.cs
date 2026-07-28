using System.Text.Json;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.Outbox;

/// <summary>
/// Registry's own outbox: it records what Registry owes the other contexts and hands those
/// records to the dispatcher. Recording joins the caller's transaction, so an event is never
/// owed for a Deletion that rolled back.
/// </summary>
internal sealed class RegistryOutbox(RegistryDbContext dbContext)
    : IIntegrationEventPublisher, IIntegrationEventOutbox
{
    private const int MaxErrorLength = 2000;

    public async Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(integrationEvent),
            CreatedAt = now,
            NextAttemptAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingIntegrationEvent>> ReadDueAsync(
        int max, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.OutboxMessages
            .Where(m => m.ParkedAt == null && m.NextAttemptAt <= now)
            .OrderBy(m => m.CreatedAt)
            .Take(max)
            .Select(m => new PendingIntegrationEvent(m.Id, m.EventType, m.Payload, m.Attempts))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkDeliveredAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task RescheduleAsync(
        Guid id, DateTimeOffset nextAttemptAt, string error, CancellationToken cancellationToken)
    {
        var lastError = Truncate(error);
        await dbContext.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.NextAttemptAt, nextAttemptAt)
                    .SetProperty(x => x.LastError, lastError),
                cancellationToken);
    }

    public async Task ParkAsync(Guid id, string error, CancellationToken cancellationToken)
    {
        var lastError = Truncate(error);
        var parkedAt = DateTimeOffset.UtcNow;
        await dbContext.OutboxMessages
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.ParkedAt, parkedAt)
                    .SetProperty(x => x.LastError, lastError),
                cancellationToken);
    }

    private static string Truncate(string error) =>
        error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
}
