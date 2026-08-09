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
    Guid ServiceId,
    /// <summary>Which watch opened it: Errors logged, or a Health Check that stopped answering.</summary>
    string Watch,
    /// <summary>Open, Quieted, Solved or Muted.</summary>
    string State,
    /// <summary>What makes this one kind of trouble rather than another — the message template, or the body with its varying parts blanked.</summary>
    string Fingerprint,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastMatchAt,
    DateTimeOffset? ClosedAt,
    int ErrorCount,
    int WarnCount,
    /// <summary>The Log Record that opened it — hand this to get_log_record.</summary>
    long? FirstMatchLogId,
    string? FirstMatchDetail,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? SolvedAt);

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
