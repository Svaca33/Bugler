namespace Bugler.Ai;

/// <summary>
/// Where the transport asks which AI settings apply right now — asked again at every completion,
/// so a change takes effect without a restart. The default source reads the Ai configuration
/// section; the Host swaps in the store behind the admin screen, which wins from the moment
/// anything was saved there (ADR 0027, on ADR 0014's terms).
/// </summary>
public interface IAiSettingsSource
{
    ValueTask<AiSettings> GetCurrentAsync(CancellationToken cancellationToken);
}
