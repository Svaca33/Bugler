using Bugler.SharedKernel;

namespace Bugler.Registry.Catalog;

/// <summary>
/// The credential proving an export comes from a specific Service.
/// Only the hash is stored; the plaintext is shown once at issue and never again.
/// A Service may hold several live keys at once, so one can be replaced without a gap in ingest.
/// </summary>
public sealed class ApiKey
{
    public required Guid Id { get; init; }
    public required ServiceId ServiceId { get; init; }
    public required byte[] KeyHash { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}
