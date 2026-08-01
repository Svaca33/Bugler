namespace Bugler.Mail;

/// <summary>
/// Where the transport asks which SMTP settings apply right now — asked again at every send, so
/// a change takes effect without a restart. The default source reads the Mail:Smtp configuration
/// section; the Host swaps in the store behind the admin screen, which wins from the moment
/// anything was saved there (ADR 0014).
/// </summary>
public interface ISmtpSettingsSource
{
    ValueTask<SmtpSettings> GetCurrentAsync(CancellationToken cancellationToken);
}
