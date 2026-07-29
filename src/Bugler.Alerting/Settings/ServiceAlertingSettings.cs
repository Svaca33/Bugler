using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>
/// A Service's overrides of its Application's detection settings; null means inherit. A row with
/// nothing overridden is deleted rather than kept, so "absent = inherit" stays the single truth.
/// </summary>
public sealed class ServiceAlertingSettings
{
    public required ServiceId ServiceId { get; init; }

    /// <summary>Stored redundantly (a Service never moves between Applications) so the admin read and the deletion cascade are single indexed queries.</summary>
    public required ApplicationId ApplicationId { get; init; }

    public Sensitivity? Sensitivity { get; set; }
    public int? QuietWindowMinutes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
