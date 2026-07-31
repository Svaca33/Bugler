namespace Bugler.Alerting.Deliveries;

/// <summary>
/// One message owed to one recipient over one channel (see CONTEXT.md: Delivery), written in the
/// same transaction as the Episode change that owes it and pursued until it succeeds or its
/// time-to-live lapses.
/// </summary>
public sealed class Delivery
{
    public required Guid Id { get; init; }
    public required Guid EpisodeId { get; init; }
    public required DeliveryKind Kind { get; init; }
    public required DeliveryChannel Channel { get; init; }

    /// <summary>The subscribed User for mail; null for chat, whose destination is the Application's current webhook.</summary>
    public Guid? UserId { get; init; }

    public int Attempts { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? LapsedAt { get; set; }
    public string? LastError { get; set; }
}

public enum DeliveryKind : short
{
    Alert = 1,

    /// <summary>Historical only: ADR 0003 retired the All Clear. Old rows keep the value; nothing writes it.</summary>
    AllClear = 2,
}

public enum DeliveryChannel : short
{
    Mail = 1,
    Chat = 2,
}
