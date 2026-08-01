namespace Bugler.Alerting.Episodes;

/// <summary>
/// What a hand laid on an Episode came to. Only Acted changed anything, and only Acted is
/// written to the Journal (see CONTEXT.md: Journal) — the Journal records acts, not requests.
/// </summary>
public enum HandOutcome
{
    /// <summary>The Episode refuses the hand (a Solved Episode takes none). The API answers 409.</summary>
    Refused,

    /// <summary>Not an act: re-acknowledging one's own mark, withdrawing none. Nothing changes anywhere.</summary>
    Nothing,

    /// <summary>The hand changed the Episode; the Journal gets its entry.</summary>
    Acted,
}
