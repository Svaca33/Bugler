namespace Bugler.Alerting.Episodes;

/// <summary>
/// What a machine hand laid on an Episode came to. Refusals are told apart because the machine
/// door repeats the reason to an agent that cannot see the screen — each one names what stood in
/// the way. Only Acted and Renewed change anything; only they reach the Journal.
/// </summary>
public enum MachineHandOutcome
{
    /// <summary>The hand changed the Episode; the Journal gets its entry.</summary>
    Acted,

    /// <summary>The holder claimed again: the lease runs anew from now, the mark keeps its moment.</summary>
    Renewed,

    /// <summary>Not an act — releasing a claim one does not hold. Nothing changes anywhere.</summary>
    Nothing,

    /// <summary>The Episode is no longer open; a claim holds nothing shut.</summary>
    RefusedClosed,

    /// <summary>The verdict has been rendered; no hand lands after it.</summary>
    RefusedSolved,

    /// <summary>A person has the kind of trouble on — the machine never touches a human-Acknowledged Episode.</summary>
    RefusedAcknowledged,

    /// <summary>Another machine's claim stands; the refusal says whose.</summary>
    RefusedHeldByAnother,

    /// <summary>A Resignation stands: a machine said this is not one it can fix, and only a human hand clears that.</summary>
    RefusedResigned,

    /// <summary>The verb belongs to the claim-holder, and the caller is not it.</summary>
    RefusedNotHolder,
}
