namespace Bugler.Registry.Outbox;

/// <summary>
/// An integration event Registry owes to the other contexts, recorded in the same transaction
/// as the fact it announces. A delivered message is deleted, so a row present here is either
/// still owed or parked after too many failures (ADR 0008).
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>Type name of the event, as the dispatcher knows it.</summary>
    public required string EventType { get; init; }

    public required string Payload { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>How many deliveries have been tried and failed.</summary>
    public int Attempts { get; set; }

    /// <summary>Not to be delivered before this moment — how the backoff is held.</summary>
    public required DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Set once delivery was given up on; such a message is never tried again.</summary>
    public DateTimeOffset? ParkedAt { get; set; }

    public string? LastError { get; set; }
}
