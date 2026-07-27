using Bugler.SharedKernel;

namespace Bugler.Registry.Catalog;

/// <summary>A single client deployment of an Application; owns its API key and retention.</summary>
public sealed class Instance
{
    public required InstanceId Id { get; init; }
    public required ApplicationId ApplicationId { get; init; }
    public required string Name { get; set; }

    /// <summary>Retention override in days; null means the server default applies.</summary>
    public int? RetentionDays { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }
}
