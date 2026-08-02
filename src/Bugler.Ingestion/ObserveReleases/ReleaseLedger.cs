using Bugler.SharedKernel;

namespace Bugler.Ingestion.ObserveReleases;

/// <summary>What a sighting turned out to be worth recording (ADR 0016).</summary>
/// <param name="PreviousVersion">
/// Null where the Service had no known version to leave: the row establishes what it was already
/// running and is not a Release, because nothing preceded it.
/// </param>
public sealed record RecordableRelease(
    ServiceId ServiceId,
    string Version,
    string? PreviousVersion,
    DateTime ObservedAt);

/// <summary>
/// Decides which sightings are Releases. Pure and single-threaded by contract — the recorder owns
/// one and drives it from one loop — so the whole rule can be read, and tested, without a database.
///
/// The rule is that a Service has one Declared Version it is regarded as running, and a set of
/// others still arriving. A rolling deploy keeps the version it replaced arriving for minutes;
/// a canary keeps it arriving for weeks; neither is a second Release, so both are absorbed by that
/// set for as long as they keep being seen. The version regarded as running never ages out of it,
/// which is what lets a Service that logs once a week stay silent instead of announcing itself
/// again every time.
///
/// The one case it gets wrong on purpose: a rollback while the old version is still trickling in
/// looks exactly like a straggler and is passed over. Preferred to the alternative, which is a
/// false Release on every rolling deploy — one marker where nothing was deployed costs more than
/// one missing marker for something the reader did by hand ten minutes ago.
/// </summary>
internal sealed class ReleaseLedger(TimeSpan stragglerWindow)
{
    /// <summary>
    /// The most versions of one Service held as still-arriving. Bounds what a sender declaring a
    /// fresh version on every export can make this cost; a Service genuinely running more than
    /// this many at once is beyond anything a marker could usefully describe.
    /// </summary>
    private const int MaxStragglersPerService = 32;

    private readonly Dictionary<ServiceId, ServiceVersions> _services = [];

    /// <summary>
    /// Restores what was recorded before this process started. Without it a restart would take
    /// every Service for unknown and write a second row establishing the version it already knew.
    /// A restart during a rolling deploy is the case the straggler carries: the version just
    /// replaced is still arriving, and it is only absorbed if it is still remembered.
    /// </summary>
    public void Seed(ServiceId serviceId, string version, string? previousVersion, DateTime recordedAt, DateTime now)
    {
        var state = new ServiceVersions { Running = version };

        if (previousVersion is not null && now - recordedAt <= stragglerWindow)
        {
            state.Straggling[previousVersion] = recordedAt;
        }

        _services[serviceId] = state;
    }

    /// <summary>
    /// Weighs one sighting. Returns what to record, or null when there is nothing new in it.
    /// Nothing is committed here: a returned row is only believed once it is stored, so a failed
    /// write leaves the ledger as it was and the next sighting offers it again.
    /// </summary>
    public RecordableRelease? Admit(ServiceVersionSighting sighting, DateTime now)
    {
        if (!_services.TryGetValue(sighting.ServiceId, out var state))
        {
            state = new ServiceVersions();
            _services[sighting.ServiceId] = state;
        }

        state.Forget(now - stragglerWindow);

        if (state.Running == sighting.Version)
        {
            return null;
        }

        if (state.Straggling.ContainsKey(sighting.Version))
        {
            // Still arriving, so still running somewhere: a straggler of a rollout, or the larger
            // half of a canary. Seen again means seen now — the window is on quiet, not on age.
            state.Straggling[sighting.Version] = now;
            return null;
        }

        return new RecordableRelease(
            sighting.ServiceId, sighting.Version, state.Running, sighting.EarliestSignal);
    }

    /// <summary>Believes a <see cref="Admit"/> that reached the database.</summary>
    public void Commit(RecordableRelease release, DateTime now)
    {
        var state = _services[release.ServiceId];

        if (state.Running is { } replaced)
        {
            state.Straggling[replaced] = now;
        }

        state.Running = release.Version;
        state.Straggling.Remove(release.Version);
        state.Forget(now - stragglerWindow);
    }

    private sealed class ServiceVersions
    {
        /// <summary>The Declared Version the Service is regarded as running; null until one is known.</summary>
        public string? Running { get; set; }

        /// <summary>Versions still arriving besides it, against the instant each was last seen.</summary>
        public Dictionary<string, DateTime> Straggling { get; } = [];

        public void Forget(DateTime quietSince)
        {
            foreach (var version in Straggling.Where(e => e.Value <= quietSince).Select(e => e.Key).ToList())
            {
                Straggling.Remove(version);
            }

            while (Straggling.Count > MaxStragglersPerService)
            {
                Straggling.Remove(Straggling.MinBy(entry => entry.Value).Key);
            }
        }
    }
}
