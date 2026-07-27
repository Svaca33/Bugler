using Bugler.SharedKernel;

namespace Bugler.Registry.Catalog;

/// <summary>
/// The credential proving an export comes from a specific Instance.
/// Only the hash is stored; the plaintext is shown once at issue and never again.
/// </summary>
public sealed class ApiKey
{
    public required Guid Id { get; init; }
    public required InstanceId InstanceId { get; init; }
    public required byte[] KeyHash { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}
