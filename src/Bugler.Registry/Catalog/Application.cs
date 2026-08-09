using Bugler.SharedKernel;

namespace Bugler.Registry.Catalog;

/// <summary>A product whose telemetry Bugler collects; the unit of user read access.</summary>
public sealed class Application
{
    public required ApplicationId Id { get; init; }
    public required string Name { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The Application's standing permission for its telemetry to be shown to the configured AI
    /// provider (see CONTEXT.md: AI Consent). Off until an Admin turns it on — for existing rows
    /// the column default says the same (ADR 0028).
    /// </summary>
    public bool AiConsent { get; set; }
}
