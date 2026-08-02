using System.Collections.Concurrent;
using System.Threading.Channels;
using Bugler.SharedKernel;

namespace Bugler.Ingestion.ObserveReleases;

/// <summary>
/// Carries version sightings off the receiving thread, like <see cref="Storage.TelemetryBuffer"/>
/// carries Signals. Two things keep it from costing what the telemetry costs: the receivers hand
/// over one sighting per OTLP Resource block rather than per Signal, and a block declaring the
/// version already handed over is dropped here without touching the channel — so a Service sending
/// steadily on one version enqueues nothing at all after its first block.
/// </summary>
internal sealed class ReleaseObservations
{
    /// <summary>
    /// Deep enough to hold every registered Service changing version at once several times over,
    /// which is more than a deployment can ever look like. Full means an observation is dropped —
    /// the gate is what makes that harmless.
    /// </summary>
    private const int Capacity = 1024;

    private readonly ConcurrentDictionary<ServiceId, string> _handedOver = new();

    private readonly Channel<ServiceVersionSighting> _sightings =
        Channel.CreateBounded<ServiceVersionSighting>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    public ChannelReader<ServiceVersionSighting> Sightings => _sightings.Reader;

    public void Observe(ServiceId serviceId, IReadOnlyList<DeclaredVersionSighting> sightings)
    {
        foreach (var sighting in sightings)
        {
            if (_handedOver.TryGetValue(serviceId, out var last) && last == sighting.Version)
            {
                continue;
            }

            // Remembered only once it is actually on the channel: a dropped sighting has to look
            // unseen, or a full channel would swallow a Release rather than delay it. The next
            // block carrying that version offers it again.
            if (_sightings.Writer.TryWrite(new ServiceVersionSighting(
                    serviceId, sighting.Version, sighting.EarliestSignal)))
            {
                _handedOver[serviceId] = sighting.Version;
            }
        }
    }
}
