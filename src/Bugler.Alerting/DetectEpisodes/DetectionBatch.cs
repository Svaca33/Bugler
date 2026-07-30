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
    string? EventName)
{
    /// <summary>The kind of trouble this record announces — the key an Episode groups by.</summary>
    public string Fingerprint => DetectEpisodes.Fingerprint.Of(Template, EventName, Body);
}

/// <summary>What one poll page decided for one kind of trouble in one Service.</summary>
public sealed record ServiceDetection(
    ServiceId ServiceId,
    ApplicationId ApplicationId,
    string Fingerprint,
    MatchingLog? OpensWith,
    int ErrorCount,
    int WarnCount);

public sealed record DetectionDecisions(
    IReadOnlyList<ServiceDetection> Services,
    IReadOnlyList<long> MatchedIds);

/// <summary>
/// The detection state machine, free of I/O: given the rows a poll read, the current effective
/// settings, which kinds of trouble already hold an open Episode, and which ids the overlap
/// re-read has already processed, decide what happens — nothing else does.
/// </summary>
public static class DetectionBatch
{
    /// <summary>
    /// A Service may hold this many open Episodes before further kinds fold into the
    /// <see cref="Fingerprint.Overflow"/> Episode — the guard against a body the normalizer
    /// cannot tame turning every Log Record into its own "kind".
    /// </summary>
    public const int MaxOpenEpisodesPerService = 25;

    public static DetectionDecisions Decide(
        IReadOnlyList<MatchingLog> rows,
        EffectiveSettings effective,
        IReadOnlySet<(ServiceId ServiceId, string Fingerprint)> openEpisodes,
        IReadOnlySet<long> seenIds)
    {
        var accumulators = new Dictionary<(ServiceId, string), Accumulator>();
        var openPerService = openEpisodes
            .GroupBy(key => key.ServiceId)
            .ToDictionary(group => group.Key, group => group.Count());
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
            if (floor is null || row.Severity < floor)
            {
                continue;
            }

            matchedIds.Add(row.Id);
            var fingerprint = row.Fingerprint;
            var key = (serviceId, fingerprint);
            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                if (openEpisodes.Contains(key))
                {
                    accumulator = new Accumulator(
                        effective.ApplicationOf(serviceId)!.Value, opensWith: null);
                }
                else if (openPerService.GetValueOrDefault(serviceId) >= MaxOpenEpisodesPerService
                    && fingerprint != Fingerprint.Overflow)
                {
                    // Too many kinds already open: this one goes to the overflow Episode.
                    key = (serviceId, Fingerprint.Overflow);
                    if (!accumulators.TryGetValue(key, out accumulator))
                    {
                        accumulator = new Accumulator(
                            effective.ApplicationOf(serviceId)!.Value,
                            openEpisodes.Contains(key) ? null : row);
                        accumulators[key] = accumulator;
                        if (accumulator.OpensWith is not null)
                        {
                            openPerService[serviceId] =
                                openPerService.GetValueOrDefault(serviceId) + 1;
                        }
                    }
                }
                else
                {
                    accumulator = new Accumulator(
                        effective.ApplicationOf(serviceId)!.Value, opensWith: row);
                    openPerService[serviceId] = openPerService.GetValueOrDefault(serviceId) + 1;
                }

                accumulators[key] = accumulator;
            }

            if (row.Severity >= 17)
            {
                accumulator.ErrorCount++;
            }
            else
            {
                accumulator.WarnCount++;
            }
        }

        var services = accumulators
            .Select(pair => new ServiceDetection(
                pair.Key.Item1, pair.Value.ApplicationId, pair.Key.Item2, pair.Value.OpensWith,
                pair.Value.ErrorCount, pair.Value.WarnCount))
            .ToList();
        return new DetectionDecisions(services, matchedIds);
    }

    private sealed class Accumulator(ApplicationId applicationId, MatchingLog? opensWith)
    {
        public ApplicationId ApplicationId { get; } = applicationId;

        /// <summary>The first matching Log Record when no Episode of this kind was open — rows arrive in id order, so first wins.</summary>
        public MatchingLog? OpensWith { get; } = opensWith;

        public int ErrorCount { get; set; }
        public int WarnCount { get; set; }
    }
}
