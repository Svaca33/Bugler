using System.Text.Json.Serialization;

namespace Bugler.Alerting.Mcp;

/// <summary>
/// One Episode as the machine door reports it — narrower than the list row the UI draws, because
/// what a reader needs here is what happened, where, since when, and how to reach the evidence
/// (ADR 0031). Everything the browser needs to lay out a timeline stays in the browser.
/// </summary>
public sealed record EpisodeSummary(
    Guid Id,
    Guid ApplicationId,
    /// <summary>The Service whose match opened it. An Episode may be fed by several — see Participations.</summary>
    Guid? OpenedByServiceId,
    /// <summary>Which watch opened it: Errors logged, or a Health Check that stopped answering.</summary>
    string Watch,
    /// <summary>Open, Quieted, Solved or Muted.</summary>
    string State,
    /// <summary>An opaque token standing for one kind of trouble; pass it back to list every episode of that kind.</summary>
    string Fingerprint,
    /// <summary>The readable name of the trouble, taken from the opening match — what the fingerprint stands for.</summary>
    string Title,
    /// <summary>Which Services and versions fed it, and how much each put in.</summary>
    IReadOnlyList<ParticipationMark> Participations,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastMatchAt,
    DateTimeOffset? ClosedAt,
    int ErrorCount,
    int WarnCount,
    /// <summary>The Log Record that opened it — hand this to get_log_record.</summary>
    long? FirstMatchLogId,
    string? FirstMatchDetail,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? SolvedAt,
    /// <summary>The machine hand's marks, where one stands — so agents see each other's work.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ClaimMark? Claim,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    NoteMark? Note,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProposalMark? Proposal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ResignationMark? Resignation);

/// <summary>
/// What one Service running one version put into an Episode. The answer to "is it still happening
/// on the version we just shipped, and is it every deployment or only one" — read the versions
/// before concluding that a fix held.
/// </summary>
public sealed record ParticipationMark(
    Guid ServiceId,
    string? Version,
    DateTimeOffset FirstAt,
    DateTimeOffset LastAt,
    int ErrorCount,
    int WarnCount);

/// <summary>The Machine Claim on an Episode: whose delegation, since when, and how long the lease still runs.</summary>
public sealed record ClaimMark(
    string? Holder, string? HolderEmail, DateTimeOffset At, DateTimeOffset LeaseUntil);

/// <summary>The claim-holder's pinned note; By is the delegation's name.</summary>
public sealed record NoteMark(string? Text, string? Link, DateTimeOffset At, string? By);

/// <summary>
/// The Solved Proposal, aged in matches rather than minutes: 0 since is persuasive, 400 is a
/// rejection waiting to happen. Overtaken means a newer Episode of the kind exists — the fix did
/// not hold and the proposal can no longer be confirmed.
/// </summary>
public sealed record ProposalMark(
    string? PrLink, DateTimeOffset At, int MatchesSince, bool Overtaken, string? By);

/// <summary>
/// A machine's statement about itself: this trouble is not one it can fix — with the reason.
/// While it stands, no machine claims this Episode; only a person sweeps it aside. Overtaken
/// means the kind returned in a newer Episode; read the history before retrying anything.
/// </summary>
public sealed record ResignationMark(string Reason, DateTimeOffset At, bool Overtaken, string? By);

public sealed record EpisodeListAnswer(
    IReadOnlyList<EpisodeSummary> Episodes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Note);

/// <summary>One human hand laid on an Episode, and when.</summary>
public sealed record JournalMark(string Kind, DateTimeOffset At);

/// <summary>
/// An Episode's Reading, carried with the sentence that says what it is. The caveat is a field
/// rather than a convention because this answer is about to be read by a model that has no other
/// way of telling generated prose from stored evidence.
/// </summary>
public sealed record MachineReading(
    string Text,
    string? Model,
    DateTimeOffset WrittenAt,
    string Caveat);

public sealed record EpisodeDetailAnswer(
    EpisodeSummary Episode,
    IReadOnlyList<JournalMark> Journal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    MachineReading? Reading);
