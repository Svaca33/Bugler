using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

/// <summary>The hands a Journal entry can record, flesh or machine (see CONTEXT.md: Journal).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JournalEntryKind>))]
public enum JournalEntryKind : short
{
    /// <summary>Somebody took the kind of trouble on — a take-over reads as one of these after another.</summary>
    Acknowledged = 1,

    /// <summary>An acknowledgement ended by hand — the holder's own, anyone else's, or a Solve consuming it.</summary>
    Withdrawn = 2,

    /// <summary>The terminal verdict landed.</summary>
    Solved = 3,

    /// <summary>A machine laid its claim (see CONTEXT.md: Machine Claim).</summary>
    Claimed = 4,

    /// <summary>The holder claimed again; the lease runs anew.</summary>
    ClaimRenewed = 5,

    /// <summary>The holder gave the Episode back deliberately.</summary>
    ClaimReleased = 6,

    /// <summary>The lease ran out — or the Episode closed under the claim — and the mark fell off by itself.</summary>
    ClaimLapsed = 7,

    /// <summary>A human hand landed over the claim; the entry names the person and the delegation displaced.</summary>
    ClaimDisplaced = 8,

    /// <summary>The holder pinned its note (see CONTEXT.md: Machine Note).</summary>
    NotePinned = 9,

    /// <summary>The holder laid its Solved Proposal (see CONTEXT.md: Solved Proposal).</summary>
    ProposalLaid = 10,

    /// <summary>A person rejected the proposal; the claim fell with it.</summary>
    ProposalRejected = 11,

    /// <summary>The holder resigned: not a machine's to fix (see CONTEXT.md: Resignation).</summary>
    Resigned = 12,

    /// <summary>A person swept the Resignation aside; machines may claim again.</summary>
    ResignationDismissed = 13,
}
