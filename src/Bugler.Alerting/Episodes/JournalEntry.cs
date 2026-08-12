namespace Bugler.Alerting.Episodes;

/// <summary>
/// One line of an Episode's Journal (see CONTEXT.md: Journal): which hand landed, whose, and when.
/// Only ever appended — the live marks say what holds now, these say what happened — and entries
/// die with their Episode.
/// </summary>
public sealed class JournalEntry
{
    public long Id { get; init; }
    public required Guid EpisodeId { get; init; }
    public required JournalEntryKind Kind { get; init; }

    /// <summary>
    /// Whose hand — for a machine entry, the User the delegation acts in the name of; for
    /// <see cref="JournalEntryKind.ClaimDisplaced"/>, the person whose hand landed over it.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The Machine Delegation the hand belonged to, where it was a machine's — and on the entries
    /// a person writes over a machine's mark (displaced, rejected, dismissed), the delegation
    /// whose mark it was, so one line names both hands.
    /// </summary>
    public Guid? DelegationId { get; init; }

    public required DateTimeOffset At { get; init; }
}
