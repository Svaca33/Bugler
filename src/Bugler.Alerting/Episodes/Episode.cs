using Bugler.SharedKernel;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// One bounded stretch of trouble in one Service (see CONTEXT.md: Episode). Carries everything a
/// message about it needs — Deliveries never read the evidence again, so an Alert survives the
/// Purge of the Log Records that drove it.
/// </summary>
public sealed class Episode
{
    /// <summary>What a Machine Note or a Resignation reason may hold — short annotations, not essays.</summary>
    public const int MaxMachineTextLength = 2000;

    /// <summary>What a pinned or proposed link may hold.</summary>
    public const int MaxMachineLinkLength = 1000;

    public required Guid Id { get; init; }
    public required ServiceId ServiceId { get; init; }

    /// <summary>Stored redundantly (immutable mapping) so visibility filtering and cascades need no catalog round-trip.</summary>
    public required ApplicationId ApplicationId { get; init; }

    /// <summary>Which watch found this trouble and keeps feeding it (see CONTEXT.md: Watch).</summary>
    public required Watch Watch { get; init; }

    /// <summary>The kind of trouble this Episode is about — what tells it apart from the Service's other Episodes.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Detection wall-clock, not the evidence's own claim — durations stay immune to sender clock skew.</summary>
    public required DateTimeOffset OpenedAt { get; init; }

    /// <summary>The Log Record the opening match was, where the Watch deals in Log Records; null where it does not.</summary>
    public long? FirstMatchLogId { get; init; }

    /// <summary>When the opening match happened by its own reckoning — the log's timestamp, the probe's moment.</summary>
    public required DateTimeOffset FirstMatchAt { get; init; }

    /// <summary>The opening match's Severity Band, where the Watch has one; null where it does not.</summary>
    public short? FirstMatchSeverity { get; init; }

    /// <summary>What the opening match said, in one line — a log's body, or what a probe got back.</summary>
    public string? FirstMatchDetail { get; init; }

    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }

    /// <summary>Processing wall-clock of the newest match; the Quiet Window is measured from here.</summary>
    public DateTimeOffset LastMatchAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>How the stretch stopped taking matches; the display state is derived, never stored twice (ADR 0003).</summary>
    public EpisodeCloseReason? CloseReason { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    public DateTimeOffset? SolvedAt { get; private set; }
    public Guid? SolvedByUserId { get; private set; }

    // The machine hand's live marks (see CONTEXT.md: Machine Claim, Machine Note, Solved
    // Proposal, Resignation). Like the acknowledgement they say only what holds now; what
    // happened is the Journal's to tell.

    /// <summary>The claim: a machine's visible, exclusive-among-machines hold, a lease.</summary>
    public Guid? ClaimedByDelegationId { get; private set; }

    /// <summary>The User the claiming delegation acts in the name of — who answers for the lapse entry.</summary>
    public Guid? ClaimedByUserId { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>When the lease runs out unless a machine write renews it. A wilted mark, never a zombie Episode.</summary>
    public DateTimeOffset? ClaimLeaseUntil { get; private set; }

    /// <summary>The one note the claim-holder may pin; pinning again replaces it.</summary>
    public string? NoteText { get; private set; }

    public string? NoteLink { get; private set; }
    public Guid? NoteByDelegationId { get; private set; }
    public DateTimeOffset? NotedAt { get; private set; }

    /// <summary>The Solved Proposal: the claim-holder's stated belief that the cause is fixed.</summary>
    public Guid? ProposalByDelegationId { get; private set; }

    public DateTimeOffset? ProposedAt { get; private set; }
    public string? ProposalLink { get; private set; }

    /// <summary>The match tally when the proposal was laid — what "matches since" is measured against.</summary>
    public int? ProposalMatchesWhenLaid { get; private set; }

    /// <summary>The Resignation: a machine's finding about itself that this trouble is not one it can fix.</summary>
    public Guid? ResignedByDelegationId { get; private set; }

    public DateTimeOffset? ResignedAt { get; private set; }
    public string? ResignationReason { get; private set; }

    /// <summary>Not mapped (no setter): Solved wins, then whether — and how — the stretch ended.</summary>
    public EpisodeState State =>
        SolvedAt is not null ? EpisodeState.Solved
        : ClosedAt is null ? EpisodeState.Open
        : CloseReason is EpisodeCloseReason.QuietWindow ? EpisodeState.Quieted
        : EpisodeState.Muted;

    /// <summary>
    /// Takes the Episode on — or over: one slot, last hand wins (see CONTEXT.md: Acknowledged).
    /// Refused on a Solved Episode, which is never Acknowledged; the holder re-acknowledging is
    /// not an act — the mark keeps its original moment, so the Journal stays its full explanation.
    /// The human hand always wins: any machine claim is shed on the way in, so the two marks
    /// never coexist — a caller who wants the displacement journaled reads the claim first.
    /// </summary>
    public HandOutcome Acknowledge(Guid userId, DateTimeOffset now)
    {
        if (SolvedAt is not null)
        {
            return HandOutcome.Refused;
        }

        if (AcknowledgedByUserId == userId)
        {
            return HandOutcome.Nothing;
        }

        AcknowledgedByUserId = userId;
        AcknowledgedAt = now;
        ShedClaim();
        return HandOutcome.Acted;
    }

    /// <summary>Withdraws the acknowledgement — the live claim ends; what happened is the Journal's to tell.</summary>
    public HandOutcome Unacknowledge()
    {
        if (AcknowledgedByUserId is null)
        {
            return HandOutcome.Nothing;
        }

        AcknowledgedByUserId = null;
        AcknowledgedAt = null;
        return HandOutcome.Acted;
    }

    /// <summary>
    /// The terminal human verdict (see CONTEXT.md: Solved): ends an open Episode on the spot and
    /// consumes any acknowledgement — and every machine mark with it: confirming a Solved
    /// Proposal is this very verdict, so the claim, the proposal and any Resignation are spent
    /// the moment it lands. Refused when already Solved — the verdict is rendered once.
    /// </summary>
    public HandOutcome Solve(Guid userId, DateTimeOffset now)
    {
        if (SolvedAt is not null)
        {
            return HandOutcome.Refused;
        }

        if (ClosedAt is null)
        {
            ClosedAt = now;
            CloseReason = EpisodeCloseReason.Solved;
        }

        SolvedByUserId = userId;
        SolvedAt = now;
        Unacknowledge();
        ShedClaim();
        ClearProposal();
        ClearResignation();
        return HandOutcome.Acted;
    }

    /// <summary>
    /// Lays or renews the claim (see CONTEXT.md: Machine Claim). Only an open Episode with no
    /// human acknowledgement, no standing Resignation and no other machine's claim takes one;
    /// that it is the newest of its kind is the caller's to check — the Episode cannot see its
    /// siblings. An expired lease still refuses a stranger: the sweep journals the lapse first,
    /// on the next beat at the latest, so no claim ever vanishes without its line.
    /// </summary>
    public MachineHandOutcome Claim(Guid delegationId, Guid userId, DateTimeOffset now, TimeSpan lease)
    {
        if (SolvedAt is not null)
        {
            return MachineHandOutcome.RefusedSolved;
        }

        if (ClosedAt is not null)
        {
            return MachineHandOutcome.RefusedClosed;
        }

        if (AcknowledgedByUserId is not null)
        {
            return MachineHandOutcome.RefusedAcknowledged;
        }

        if (ResignedAt is not null)
        {
            return MachineHandOutcome.RefusedResigned;
        }

        if (ClaimedByDelegationId is { } holder && holder != delegationId)
        {
            return MachineHandOutcome.RefusedHeldByAnother;
        }

        var renewal = ClaimedByDelegationId == delegationId;
        ClaimedByDelegationId = delegationId;
        ClaimedByUserId = userId;
        ClaimedAt = renewal ? ClaimedAt : now;
        ClaimLeaseUntil = now + lease;
        return renewal ? MachineHandOutcome.Renewed : MachineHandOutcome.Acted;
    }

    /// <summary>Gives the Episode back deliberately. Releasing a claim one does not hold is nothing.</summary>
    public MachineHandOutcome ReleaseClaim(Guid delegationId)
    {
        if (ClaimedByDelegationId != delegationId)
        {
            return MachineHandOutcome.Nothing;
        }

        ShedClaim();
        return MachineHandOutcome.Acted;
    }

    /// <summary>
    /// Clears the claim and says whose it was — the caller writes the Journal line that names
    /// why (released, lapsed, displaced). Null when there was none to shed.
    /// </summary>
    public (Guid DelegationId, Guid UserId)? ShedClaim()
    {
        if (ClaimedByDelegationId is not { } delegation || ClaimedByUserId is not { } user)
        {
            return null;
        }

        ClaimedByDelegationId = null;
        ClaimedByUserId = null;
        ClaimedAt = null;
        ClaimLeaseUntil = null;
        return (delegation, user);
    }

    /// <summary>
    /// Pins the note — the claim-holder's alone, and pinning again replaces it (see CONTEXT.md:
    /// Machine Note). A machine write; the lease runs anew.
    /// </summary>
    public MachineHandOutcome PinNote(
        Guid delegationId, string? text, string? link, DateTimeOffset now, TimeSpan lease)
    {
        if (ClaimedByDelegationId != delegationId)
        {
            return MachineHandOutcome.RefusedNotHolder;
        }

        NoteText = text;
        NoteLink = link;
        NoteByDelegationId = delegationId;
        NotedAt = now;
        ClaimLeaseUntil = now + lease;
        return MachineHandOutcome.Acted;
    }

    /// <summary>
    /// Lays the Solved Proposal (see CONTEXT.md: Solved Proposal) — the claim-holder's alone,
    /// laying again replaces it, and the match tally is remembered so its age shows in matches
    /// rather than in minutes. A machine write; the lease runs anew.
    /// </summary>
    public MachineHandOutcome ProposeSolved(
        Guid delegationId, string link, DateTimeOffset now, TimeSpan lease)
    {
        if (ClaimedByDelegationId != delegationId)
        {
            return MachineHandOutcome.RefusedNotHolder;
        }

        ProposalByDelegationId = delegationId;
        ProposedAt = now;
        ProposalLink = link;
        ProposalMatchesWhenLaid = ErrorCount + WarnCount;
        ClaimLeaseUntil = now + lease;
        return MachineHandOutcome.Acted;
    }

    /// <summary>
    /// A person saying no to the proposal: it goes, and the claim goes with it — the Episode
    /// returns to its normal lifecycle. A standing proposal falls whoever holds the claim now;
    /// the Journal names whose proposal it was.
    /// </summary>
    public HandOutcome RejectProposal()
    {
        if (ProposedAt is null)
        {
            return HandOutcome.Nothing;
        }

        ClearProposal();
        ShedClaim();
        return HandOutcome.Acted;
    }

    /// <summary>
    /// The machine's finding about itself (see CONTEXT.md: Resignation): this trouble is not one
    /// it can fix, said with the reason why. The claim ends with it — there is nothing left to
    /// hold — and no machine claims past it until a human hand sweeps it aside.
    /// </summary>
    public MachineHandOutcome Resign(Guid delegationId, string reason, DateTimeOffset now)
    {
        if (ClaimedByDelegationId != delegationId)
        {
            return MachineHandOutcome.RefusedNotHolder;
        }

        ResignedByDelegationId = delegationId;
        ResignedAt = now;
        ResignationReason = reason;
        ShedClaim();
        return MachineHandOutcome.Acted;
    }

    /// <summary>A person sweeping the Resignation aside: the machine's statement is refused, machines may claim again.</summary>
    public HandOutcome DismissResignation()
    {
        if (ResignedAt is null)
        {
            return HandOutcome.Nothing;
        }

        ClearResignation();
        return HandOutcome.Acted;
    }

    private void ClearProposal()
    {
        ProposalByDelegationId = null;
        ProposedAt = null;
        ProposalLink = null;
        ProposalMatchesWhenLaid = null;
    }

    private void ClearResignation()
    {
        ResignedByDelegationId = null;
        ResignedAt = null;
        ResignationReason = null;
    }
}
