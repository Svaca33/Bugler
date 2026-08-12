namespace Bugler.Alerting.Settings;

/// <summary>
/// An Application's detection settings. Absent row — and any null field — means the
/// <see cref="AlertingDefaults"/> apply, mirroring how retention falls back to the server default.
/// </summary>
public sealed class ApplicationAlertingSettings
{
    public required ApplicationId ApplicationId { get; init; }
    public Sensitivity? Sensitivity { get; set; }
    public int? QuietWindowMinutes { get; set; }

    /// <summary>How long this Application's Machine Claims lease for; null means the default applies.</summary>
    public int? ClaimLeaseHours { get; set; }

    /// <summary>The Chat Webhook (see CONTEXT.md) — a secret; endpoints expose only its host.</summary>
    public string? ChatWebhookUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
