using Bugler.Mail;

namespace Bugler.Host;

/// <summary>
/// The one row of SMTP settings the admin screen edits. Its existence is the switch: while the
/// table is empty, the Mail:Smtp configuration section applies; from the first save the row wins
/// whole — never field by field — until the reset action deletes it again (ADR 0014).
/// The password is stored as it will be sent: SMTP needs it back in clear, and the API never
/// returns it (write-only).
/// </summary>
public sealed class StoredSmtpSettings
{
    /// <summary>Always 1 — a single-row table, like Alerting's poll cursor.</summary>
    public int Id { get; set; }

    public string Host { get; set; } = "";
    public int Port { get; set; }
    public SmtpSecurity Security { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
