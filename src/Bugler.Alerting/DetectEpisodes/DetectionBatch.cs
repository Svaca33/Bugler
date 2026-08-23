using Bugler.Alerting.Settings;
using Bugler.SharedKernel;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>One row the telemetry poll read: a Log Record at or above the global severity floor.</summary>
public sealed record MatchingLog(
    long Id,
    Guid ServiceId,
    DateTime Timestamp,
    short Severity,
    string? Body,
    string? Template,
    string? EventName,
    /// <summary>The exception's type, where the sender declared one (`exception.type`).</summary>
    string? ExceptionType = null,
    /// <summary>The stack trace, head and tail where the poll had to cut it short (`exception.stacktrace`).</summary>
    string? ExceptionStack = null,
    /// <summary>The Runtime as the sender declares it (`telemetry.sdk.language`) — which recipe reads the stack.</summary>
    string? Runtime = null,
    /// <summary>What the sender said it was running (`service.version`) — the Participation's own version.</summary>
    string? ServiceVersion = null,
    /// <summary>The attributes some Application named for its Fingerprint Rule, where this row carries them.</summary>
    IReadOnlyDictionary<string, string>? NamedAttributes = null);

/// <summary>What one Service running one version put into one Episode over one poll page.</summary>
public sealed record ParticipantTally(
    ServiceId ServiceId, string? Version, int ErrorCount, int WarnCount)
{
    public int ErrorCount { get; set; } = ErrorCount;
    public int WarnCount { get; set; } = WarnCount;
}

/// <summary>
/// What one poll page decided for one kind of trouble in one Episode Scope. The Title, the rung
/// and the truncation mark are the opening Match's — they are stamped on the Episode when it
/// opens and never revised.
/// </summary>
public sealed record ScopeDetection(
    string ScopeKey,
    ApplicationId ApplicationId,
    string Fingerprint,
    string Title,
    FingerprintRung Rung,
    bool StackTruncated,
    MatchingLog? OpensWith,
    int ErrorCount,
    int WarnCount,
    IReadOnlyList<ParticipantTally> Participants);

public sealed record DetectionDecisions(
    IReadOnlyList<ScopeDetection> Scopes,
    IReadOnlyList<long> MatchedIds);

/// <summary>
/// The detection state machine, free of I/O: given the rows a poll read, the current effective
/// settings, which kinds of trouble already hold an open Episode in which Scope, and which ids
/// the overlap re-read has already processed, decide what happens — nothing else does.
///
/// There is no cap on how many Episodes a Scope may hold open (ADR 0034). With Fingerprints as
/// fine as ADR 0033 makes them, a cap would hide real distinct failures inside one bucket —
/// reinventing the mixed Episode this work exists to remove. What the cap really guarded was the
/// mailbox, and the Storm guards that instead.
/// </summary>
public static class DetectionBatch
{
    public static DetectionDecisions Decide(
        IReadOnlyList<MatchingLog> rows,
        EffectiveSettings effective,
        IReadOnlySet<(string ScopeKey, string Fingerprint)> openEpisodes,
        IReadOnlySet<long> seenIds)
    {
        var accumulators = new Dictionary<(string, string), Accumulator>();
        var matchedIds = new List<long>();

        foreach (var row in rows)
        {
            if (seenIds.Contains(row.Id))
            {
                continue; // The overlap re-read; this row was already judged by an earlier poll.
            }

            // The poll reads everything Warn and above; the per-Service floor decides here,
            // once per row — a later Sensitivity change never re-judges what was observed. An
            // unknown Service resolves to Off, so orphan telemetry never opens anything.
            var serviceId = new ServiceId(row.ServiceId);
            var floor = effective.SensitivityOf(serviceId).SeverityFloor();
            if (floor is null || row.Severity < floor
                || effective.ScopeKeyOf(serviceId) is not { } scopeKey)
            {
                continue;
            }

            matchedIds.Add(row.Id);
            var reading = Fingerprint.Of(
                EvidenceOf(row, effective.FingerprintAttributeKeyOf(serviceId)),
                effective.FingerprintRuleOf(serviceId));

            var key = (scopeKey, reading.Fingerprint);
            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                accumulator = new Accumulator(
                    effective.ApplicationOf(serviceId)!.Value,
                    reading,
                    // Rows arrive in id order, so the first of a kind with nothing open is the
                    // one that opens it.
                    openEpisodes.Contains(key) ? null : row);
                accumulators[key] = accumulator;
            }

            accumulator.Count(serviceId, Trimmed(row.ServiceVersion), row.Severity >= 17);
        }

        var scopes = accumulators
            .Select(pair => new ScopeDetection(
                pair.Key.Item1,
                pair.Value.ApplicationId,
                pair.Key.Item2,
                pair.Value.Reading.Title,
                pair.Value.Reading.Rung,
                pair.Value.Reading.StackTruncated,
                pair.Value.OpensWith,
                pair.Value.ErrorCount,
                pair.Value.WarnCount,
                pair.Value.Participants()))
            .ToList();
        return new DetectionDecisions(scopes, matchedIds);
    }

    private static FingerprintEvidence EvidenceOf(MatchingLog row, string? attributeKey) =>
        new(row.Template, row.EventName, row.Body, row.ExceptionType, row.ExceptionStack,
            row.Runtime,
            attributeKey is null || row.NamedAttributes is null
                ? null
                : row.NamedAttributes.GetValueOrDefault(attributeKey));

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Accumulator(
        ApplicationId applicationId, FingerprintReading reading, MatchingLog? opensWith)
    {
        private readonly Dictionary<(ServiceId, string?), ParticipantTally> _participants = [];

        public ApplicationId ApplicationId { get; } = applicationId;

        /// <summary>The opening Match's reading — the Title, the rung and the truncation mark the Episode keeps.</summary>
        public FingerprintReading Reading { get; } = reading;

        /// <summary>The first matching Log Record when no Episode of this kind was open in this Scope.</summary>
        public MatchingLog? OpensWith { get; } = opensWith;

        public int ErrorCount { get; private set; }
        public int WarnCount { get; private set; }

        public void Count(ServiceId serviceId, string? version, bool isError)
        {
            if (isError)
            {
                ErrorCount++;
            }
            else
            {
                WarnCount++;
            }

            var key = (serviceId, version);
            if (!_participants.TryGetValue(key, out var tally))
            {
                tally = new ParticipantTally(serviceId, version, 0, 0);
                _participants[key] = tally;
            }

            if (isError)
            {
                tally.ErrorCount++;
            }
            else
            {
                tally.WarnCount++;
            }
        }

        public IReadOnlyList<ParticipantTally> Participants() => _participants.Values.ToList();
    }
}
