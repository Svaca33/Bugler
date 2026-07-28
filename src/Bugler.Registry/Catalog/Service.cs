using Bugler.SharedKernel;

namespace Bugler.Registry.Catalog;

/// <summary>
/// A registered sender of telemetry — one role of one deployment of an Application.
/// Owns its API keys and its retention; its identity is what the API key proves,
/// never what an Export Request claims about itself (ADR 0006).
/// </summary>
public sealed class Service
{
    public required ServiceId Id { get; init; }
    public required ApplicationId ApplicationId { get; init; }

    /// <summary>Which deployment of the Application this Service belongs to (OTel `service.namespace`).</summary>
    public required string Namespace { get; set; }

    /// <summary>The stage this Service runs in (OTel `deployment.environment.name`).</summary>
    public required string Environment { get; set; }

    /// <summary>The role this Service plays inside its deployment (OTel `service.name`).</summary>
    public required string Name { get; set; }

    /// <summary>Retention override in days; null means the server default applies.</summary>
    public int? RetentionDays { get; set; }

    public required DateTimeOffset CreatedAt { get; init; }
}
