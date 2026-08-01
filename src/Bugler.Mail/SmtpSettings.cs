namespace Bugler.Mail;

/// <summary>
/// The SMTP settings in force for one send — a snapshot taken from the
/// <see cref="ISmtpSettingsSource"/> at the moment of asking, never a live view.
/// Empty <see cref="Username"/> means the server is not authenticated against.
/// </summary>
public sealed record SmtpSettings(
    string Host,
    int Port,
    SmtpSecurity Security,
    string Username,
    string Password,
    string From)
{
    /// <summary>Unconfigured SMTP disables mail entirely; it never fails startup.</summary>
    public bool IsConfigured => Host.Length > 0 && From.Length > 0;
}
