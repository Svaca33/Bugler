using System.Text.Json;
using Bugler.SharedKernel;

namespace Bugler.Host.IntegrationEvents;

/// <summary>
/// Drains every context's outbox and hands each event to whichever contexts registered a
/// handler for it. This is the only place that knows who listens to whom: publishers record
/// facts, handlers act on them, neither learns about the other (ADR 0008).
/// </summary>
internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    OutboxSignal signal,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;

    /// <summary>Fallback wake-up, so a lost nudge costs latency rather than delivery.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

    /// <summary>Every integration event the system knows, by the name publishers record.</summary>
    private static readonly Dictionary<string, Type> EventTypes =
        typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && type.IsAssignableTo(typeof(IIntegrationEvent)))
            .ToDictionary(type => type.Name);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox drain failed; retrying on the next tick");
            }

            await signal.WaitAsync(Tick, stoppingToken);
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        // The outboxes keep their own scope for the whole drain — they only run statements and
        // hold no tracked state. Each message gets a fresh scope, so a handler that fails cannot
        // leave a half-changed DbContext behind for the next one.
        await using var outboxScope = scopeFactory.CreateAsyncScope();

        foreach (var outbox in outboxScope.ServiceProvider.GetServices<IIntegrationEventOutbox>())
        {
            foreach (var message in await outbox.ReadDueAsync(BatchSize, cancellationToken))
            {
                await using var handlerScope = scopeFactory.CreateAsyncScope();
                await DeliverAsync(handlerScope.ServiceProvider, outbox, message, cancellationToken);
            }
        }
    }

    private async Task DeliverAsync(
        IServiceProvider handlers,
        IIntegrationEventOutbox outbox,
        PendingIntegrationEvent message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!EventTypes.TryGetValue(message.EventType, out var eventType))
            {
                throw new InvalidOperationException($"No integration event named '{message.EventType}'.");
            }

            var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType)
                ?? throw new InvalidOperationException($"Empty payload for '{message.EventType}'.");

            // Delivery is at-least-once: a second handler failing replays the first on retry,
            // which is why every handler is required to be idempotent.
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handle = handlerType.GetMethod("HandleAsync")!;
            foreach (var handler in handlers.GetServices(handlerType))
            {
                await (Task)handle.Invoke(handler, [integrationEvent, cancellationToken])!;
            }

            await outbox.MarkDeliveredAsync(message.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(outbox, message, exception, cancellationToken);
        }
    }

    private async Task RecordFailureAsync(
        IIntegrationEventOutbox outbox,
        PendingIntegrationEvent message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failures = message.Attempts + 1;

        if (OutboxRetryPolicy.ShouldPark(failures))
        {
            logger.LogError(
                exception,
                "Gave up delivering {Event} {MessageId} after {Failures} attempts; it stays in the outbox",
                message.EventType, message.Id, failures);
            await outbox.ParkAsync(message.Id, exception.ToString(), cancellationToken);
            return;
        }

        var nextAttemptAt = DateTimeOffset.UtcNow + OutboxRetryPolicy.DelayAfter(failures);
        logger.LogWarning(
            exception,
            "Delivery of {Event} {MessageId} failed ({Failures}); next attempt at {NextAttemptAt}",
            message.EventType, message.Id, failures, nextAttemptAt);
        await outbox.RescheduleAsync(message.Id, nextAttemptAt, exception.ToString(), cancellationToken);
    }
}
