using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bugler.Alerting.DeliverMessages;

/// <summary>
/// Sends over the configured SMTP server. One connection per message — SmtpClient is not
/// thread-safe and alerting volume is two messages per Episode, so pooling would buy nothing.
/// </summary>
internal sealed class MailKitMailSender(IOptions<AlertingOptions> options) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value.Smtp;

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(smtp.From));
        mime.To.Add(MailboxAddress.Parse(message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            smtp.Host, smtp.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        if (smtp.Username.Length > 0)
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
