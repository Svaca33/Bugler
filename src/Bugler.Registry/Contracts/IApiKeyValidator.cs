using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>
/// Answers the only question Ingestion may ask Registry:
/// does this API key identify a live (non-revoked) Service?
/// </summary>
public interface IApiKeyValidator
{
    /// <returns>The Service the key belongs to, or null when the key is unknown or revoked.</returns>
    ValueTask<ServiceId?> ValidateAsync(string apiKey, CancellationToken cancellationToken);
}
