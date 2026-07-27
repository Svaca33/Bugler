using Bugler.SharedKernel;

namespace Bugler.Registry.Catalog;

/// <summary>A product whose telemetry Bugler collects; the unit of user read access.</summary>
public sealed class Application
{
    public required ApplicationId Id { get; init; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
}
