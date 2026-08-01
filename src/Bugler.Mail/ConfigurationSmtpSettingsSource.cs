using Microsoft.Extensions.Options;

namespace Bugler.Mail;

/// <summary>
/// The Mail:Smtp configuration section as a settings source. On its own it is the whole story;
/// under the Host it is the fallback a stored configuration falls back to (ADR 0014).
/// </summary>
public sealed class ConfigurationSmtpSettingsSource(IOptions<MailOptions> options) : ISmtpSettingsSource
{
    public ValueTask<SmtpSettings> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var smtp = options.Value.Smtp;
        return ValueTask.FromResult(new SmtpSettings(
            smtp.Host, smtp.Port, smtp.Security, smtp.Username, smtp.Password, smtp.From));
    }
}
