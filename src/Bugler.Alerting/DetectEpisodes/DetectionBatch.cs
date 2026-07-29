using Bugler.Alerting.Settings;
using Bugler.SharedKernel;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>One row the telemetry poll read: a Log Record at or above the global severity floor.</summary>
public sealed record MatchingLog(long Id, Guid ServiceId, DateTime Timestamp, short Severity, string? Body);

/// <summary>What one poll page decided for one Service: open an Episode (with this first Log Record) or just count.</summary>
public sealed record ServiceDetection(
    ServiceId ServiceId,
    ApplicationId ApplicationId,
    MatchingLog? OpensWith,
    int ErrorCount,
    int WarnCount);

public sealed record DetectionDecisions(
    IReadOnlyList<ServiceDetection> Services,
    IReadOnlyList<long> MatchedIds);

/// <summary>
/// The detection state machine, free of I/O: given the rows a poll read, the current effective
/// settings, which Services already hold an open Episode, and which ids the overlap re-read has
/// already processed, decide what happens — nothing else does.
/// </summary>
public static class DetectionBatch
{
    public static DetectionDecisions Decide(
        IReadOnlyList<MatchingLog> rows,
        EffectiveSettings effective,
        IReadOnlySet<ServiceId> servicesWithOpenEpisode,
        IReadOnlySet<long> seenIds)
    {
        var accumulators = new Dictionary<ServiceId, Accumulator>();
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
            if (!accumulators.TryGetValue(serviceId, out var accumulator))
            {
                accumulator = new Accumulator(
                    effective.ApplicationOf(serviceId)!.Value,
                    servicesWithOpenEpisode.Contains(serviceId) ? null : row);
                accumulators[serviceId] = accumulator;
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
                pair.Key, pair.Value.ApplicationId, pair.Value.OpensWith,
                pair.Value.ErrorCount, pair.Value.WarnCount))
            .ToList();
        return new DetectionDecisions(services, matchedIds);
    }

    private sealed class Accumulator(ApplicationId applicationId, MatchingLog? opensWith)
    {
        public ApplicationId ApplicationId { get; } = applicationId;

        /// <summary>The first matching Log Record when no Episode was open — rows arrive in id order, so first wins.</summary>
        public MatchingLog? OpensWith { get; } = opensWith;

        public int ErrorCount { get; set; }
        public int WarnCount { get; set; }
    }
}
