using Bugler.SharedKernel;

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

    /// <summary>
    /// Which Service fell into the Episode, on a Joining Alert; null on every other kind. The
    /// message is for that Service's own followers, so it is that Service the message names —
    /// the Episode may have opened in a deployment they neither run nor care about.
    /// </summary>
    public ServiceId? JoiningServiceId { get; init; }

    /// <summary>
    /// How many kinds of trouble the Storm folded into this digest; null on every other kind.
    /// Counted when the digest is owed rather than when it is composed, for the same reason an
    /// Alert carries its own evidence: what the message says must not drift after it is owed.
    /// </summary>
    public int? FoldedEpisodeCount { get; init; }

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

    /// <summary>
    /// The message a Resignation owes (see CONTEXT.md: Resignation): unlike a Solved Proposal it
    /// has no PR notifying anyone on the code side, so if nobody is told, nobody comes.
    /// </summary>
    Resignation = 3,

    /// <summary>
    /// The late Alert a Service falling into a running Episode owes its own followers (ADR 0034).
    /// It says since when the Episode has been running rather than announcing an opening, because
    /// for its recipient nothing opened just now.
    /// </summary>
    Joined = 4,

    /// <summary>
    /// The one message a Storm sends in place of the Alerts it folded (see CONTEXT.md: Storm):
    /// how many kinds of trouble opened in one Episode Scope, and where to go and look.
    /// </summary>
    StormDigest = 5,
}

public enum DeliveryChannel : short
{
    Mail = 1,
    Chat = 2,
}
