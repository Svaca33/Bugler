using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>
/// One kind of trouble in one Service keeping its own Quiet Window (ADR 0004) — the third and
/// last tier under `service ?? application ?? default`. It outlives the Episodes it was set
/// from: a Fingerprint that keeps recurring is the very thing it exists to hold together.
/// Absence means inherit, so a row is deleted rather than nulled.
/// </summary>
public sealed class FingerprintQuietWindow
{
    /// <summary>
    /// Beyond a week the honest answer is Sensitivity Off or a fix — not a window. Only this
    /// tier is capped: an Application-wide year is never typed by accident, a year on the one
    /// error that woke you at 03:00 is.
    /// </summary>
    public const int MaxMinutes = 7 * 24 * 60;

    public required ServiceId ServiceId { get; init; }

    /// <summary>The kind of trouble, exactly as Episodes carry it (see CONTEXT.md: Fingerprint).</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Stored redundantly (a Service never moves between Applications) so the Deletion cascade stays one indexed query.</summary>
    public required ApplicationId ApplicationId { get; init; }

    public required int QuietWindowMinutes { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
