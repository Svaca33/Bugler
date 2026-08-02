using Bugler.SharedKernel;

namespace Bugler.Ingestion.ObserveReleases;

/// <summary>
/// One OTLP Resource block seen declaring a version, with the earliest instant its Signals carry.
/// A block is the natural unit: OTLP states the Resource once for all the Signals under it, so a
/// declared version is read once per block rather than once per Signal.
/// </summary>
/// <param name="EarliestSignal">
/// The oldest timestamp among the block's Signals, on the sender's clock. The closest thing to
/// "when the new version started running" that arriving telemetry can offer, and the clock the
/// Time Filter and the Volume are already drawn on (ADR 0016).
/// </param>
public sealed record DeclaredVersionSighting(string Version, DateTime EarliestSignal);

/// <summary>A <see cref="DeclaredVersionSighting"/> attributed to the Source that sent it.</summary>
internal sealed record ServiceVersionSighting(ServiceId ServiceId, string Version, DateTime EarliestSignal);
