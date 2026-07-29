namespace Bugler.SharedKernel;

/// <summary>
/// A fact one context announces to the others. Recorded in the same transaction as the fact
/// itself and delivered afterwards, so a context can never be told about something that did
/// not happen, nor stay silent about something that did (ADR 0008).
/// </summary>
public interface IIntegrationEvent;

/// <summary>
/// Records an <see cref="IIntegrationEvent"/> for delivery. The record joins the caller's
/// unit of work, so it commits — or rolls back — with the fact it announces.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}

/// <summary>What one context does about another context's fact. Must be idempotent: delivery is at-least-once.</summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>An event still owed to its handlers.</summary>
public sealed record PendingIntegrationEvent(Guid Id, string EventType, string Payload, int Attempts);

/// <summary>
/// The undelivered events of one publishing context. Every publisher keeps its own, in its own
/// schema, because the point of the outbox is that the record shares the publisher's transaction.
/// </summary>
public interface IIntegrationEventOutbox
{
    /// <summary>Events due for delivery now, oldest first.</summary>
    Task<IReadOnlyList<PendingIntegrationEvent>> ReadDueAsync(int max, CancellationToken cancellationToken);

    /// <summary>Delivered to every handler — the record is no longer owed and is dropped.</summary>
    Task MarkDeliveredAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Delivery failed; try again no sooner than <paramref name="nextAttemptAt"/>.</summary>
    Task RescheduleAsync(
        Guid id, DateTimeOffset nextAttemptAt, string error, CancellationToken cancellationToken);

    /// <summary>Delivery failed too often; stop trying and leave the record for an operator.</summary>
    Task ParkAsync(Guid id, string error, CancellationToken cancellationToken);
}

/// <summary>
/// Tells the dispatcher an outbox has something new, so delivery need not wait for the next tick.
/// A lost signal costs latency, never delivery.
/// </summary>
public interface IOutboxSignal
{
    void Nudge();
}

/// <summary>
/// Services were deleted from the Catalog. They can never be addressed again, so every
/// Signal they sent is to be erased.
/// </summary>
public sealed record ServicesDeleted(IReadOnlyList<ServiceId> ServiceIds) : IIntegrationEvent;

/// <summary>
/// An Application was deleted from the Catalog together with all of its Services.
/// Read access granted to it no longer refers to anything.
/// </summary>
public sealed record ApplicationDeleted(ApplicationId ApplicationId) : IIntegrationEvent;

/// <summary>
/// A User was deleted for good. Everything standing in their name elsewhere — their alerting
/// Subscriptions — dies with them, as their grants already do inside Access.
/// </summary>
public sealed record UserDeleted(Guid UserId) : IIntegrationEvent;
