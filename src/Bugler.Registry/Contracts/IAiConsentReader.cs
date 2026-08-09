using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>
/// Whether an Application's telemetry may be shown to the configured AI provider — asked at the
/// moment the data would leave, never earlier and never from a cache, so a withdrawn consent
/// stops the very next disclosure (ADR 0028). An unknown Application has no consent.
/// </summary>
public interface IAiConsentReader
{
    ValueTask<bool> HasConsentAsync(ApplicationId applicationId, CancellationToken cancellationToken);
}
