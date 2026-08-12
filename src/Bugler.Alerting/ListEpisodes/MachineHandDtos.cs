using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;

namespace Bugler.Alerting.ListEpisodes;

/// <summary>
/// Whose machine hand a mark belongs to: the delegation's name and its holder's e-mail. Both
/// null when the delegation is gone — the timestamp stays, the name is shrugged.
/// </summary>
public sealed record MachineHandByDto(string? Name, string? HolderEmail);

/// <summary>The Machine Claim as the UI shows it (see CONTEXT.md: Machine Claim).</summary>
public sealed record MachineClaimDto(DateTimeOffset At, DateTimeOffset LeaseUntil, MachineHandByDto By);

/// <summary>The claim-holder's pinned note (see CONTEXT.md: Machine Note).</summary>
public sealed record MachineNoteDto(string? Text, string? Link, DateTimeOffset At, MachineHandByDto By);

/// <summary>
/// The Solved Proposal with its age in matches — 0 is persuasive, 400 is a rejection waiting to
/// happen. Overtaken when the Episode is no longer the newest of its kind: the trouble returned,
/// the fix did not hold, and the proposal can no longer be confirmed.
/// </summary>
public sealed record SolvedProposalDto(
    string? Link, DateTimeOffset At, int MatchesSince, bool Overtaken, MachineHandByDto By);

/// <summary>
/// The Resignation (see CONTEXT.md: Resignation). Overtaken when a newer Episode of the kind
/// exists — the statement stays readable but no longer counts as standing.
/// </summary>
public sealed record ResignationDto(string Reason, DateTimeOffset At, bool Overtaken, MachineHandByDto By);

/// <summary>One place that turns an Episode's machine-hand columns into the DTOs both the list and the detail carry.</summary>
internal static class MachineHandDtos
{
    public static IEnumerable<Guid> DelegationIds(Episode episode)
    {
        if (episode.ClaimedByDelegationId is { } claim)
        {
            yield return claim;
        }

        if (episode.NoteByDelegationId is { } note)
        {
            yield return note;
        }

        if (episode.ProposalByDelegationId is { } proposal)
        {
            yield return proposal;
        }

        if (episode.ResignedByDelegationId is { } resignation)
        {
            yield return resignation;
        }
    }

    public static MachineClaimDto? Claim(
        Episode episode, IReadOnlyDictionary<Guid, MachineDelegationName> names) =>
        episode is { ClaimedByDelegationId: { } by, ClaimedAt: { } at, ClaimLeaseUntil: { } until }
            ? new MachineClaimDto(at, until, By(names, by))
            : null;

    public static MachineNoteDto? Note(
        Episode episode, IReadOnlyDictionary<Guid, MachineDelegationName> names) =>
        episode is { NoteByDelegationId: { } by, NotedAt: { } at }
            ? new MachineNoteDto(episode.NoteText, episode.NoteLink, at, By(names, by))
            : null;

    public static SolvedProposalDto? Proposal(
        Episode episode, bool newerExists, IReadOnlyDictionary<Guid, MachineDelegationName> names) =>
        episode is { ProposalByDelegationId: { } by, ProposedAt: { } at }
            ? new SolvedProposalDto(
                episode.ProposalLink,
                at,
                Math.Max(
                    0,
                    episode.ErrorCount + episode.WarnCount - (episode.ProposalMatchesWhenLaid ?? 0)),
                newerExists,
                By(names, by))
            : null;

    public static ResignationDto? Resignation(
        Episode episode, bool newerExists, IReadOnlyDictionary<Guid, MachineDelegationName> names) =>
        episode is { ResignedByDelegationId: { } by, ResignedAt: { } at }
            ? new ResignationDto(episode.ResignationReason ?? "", at, newerExists, By(names, by))
            : null;

    private static MachineHandByDto By(
        IReadOnlyDictionary<Guid, MachineDelegationName> names, Guid delegationId) =>
        names.TryGetValue(delegationId, out var name)
            ? new MachineHandByDto(name.Name, name.HolderEmail)
            : new MachineHandByDto(null, null);
}
