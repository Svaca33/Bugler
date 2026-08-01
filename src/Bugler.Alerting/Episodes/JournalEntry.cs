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
    public required Guid UserId { get; init; }
    public required DateTimeOffset At { get; init; }
}
