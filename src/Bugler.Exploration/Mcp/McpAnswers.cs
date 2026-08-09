using System.Text.Json;
using System.Text.Json.Serialization;
using Bugler.Exploration.ObservedKeys;

namespace Bugler.Exploration.Mcp;

/// <summary>
/// One Log Record as a search returns it: what a reader needs to decide whether to open it, and
/// nothing more. <see cref="Attributes"/> carries only the keys the Filter named — they are why
/// the record matched — while the rest waits behind get_log_record (ADR 0031).
/// </summary>
public sealed record LogRecordSummary(
    long Id,
    Guid ServiceId,
    DateTime Timestamp,
    string SeverityBand,
    string? Body,
    string? TraceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Attributes);

/// <summary>
/// A search's whole answer. <see cref="Matched"/> is how many Log Records the Filter matched rather
/// than how many came back, counted to a cap past which <see cref="MatchedCapped"/> stands — and
/// -1 where the count itself could not be taken in time. <see cref="Note"/> is present whenever the
/// list is a slice, because a slice that says nothing about being one gets read as the whole.
/// </summary>
public sealed record LogSearchAnswer(
    IReadOnlyList<LogRecordSummary> Items,
    int Matched,
    bool MatchedCapped,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Note);

/// <summary>One Log Record whole, attributes and all.</summary>
public sealed record LogRecordDetail(
    long Id,
    Guid ServiceId,
    DateTime Timestamp,
    string SeverityBand,
    string? SeverityText,
    string? Body,
    string? TraceId,
    string? SpanId,
    string? ScopeName,
    JsonElement ResourceAttributes,
    JsonElement Attributes);

/// <summary>The Observed Keys, with the sentence that keeps them from being read as a schema.</summary>
public sealed record ObservedKeysAnswer(IReadOnlyList<ObservedKeyDto> Keys, string Note);

/// <summary>
/// One Release: the instant a Service began reporting a version it was not already running. Read
/// beside an Episode it answers "did this start after a deploy" — the question that turns a wall of
/// log records into a cause.
/// </summary>
public sealed record ReleaseMark(
    Guid ServiceId,
    string Version,
    string? PreviousVersion,
    DateTime ObservedAt);

public sealed record ReleasesAnswer(IReadOnlyList<ReleaseMark> Releases, string? Note);

/// <summary>
/// One Span of a Trace, flattened. The Waterfall the UI draws is a hierarchy shaped for a screen;
/// what travels here is the same set with each Span naming its parent, which converts to text
/// without pretending to be a picture (ADR 0031).
/// </summary>
public sealed record TraceSpan(
    string SpanId,
    string? ParentSpanId,
    Guid ServiceId,
    string Name,
    DateTime StartTime,
    double DurationMs,
    string? StatusCode,
    string? StatusMessage);

public sealed record TraceAnswer(
    string TraceId,
    IReadOnlyList<TraceSpan> Spans,
    int SpanCount,
    string? Note);
