using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bugler.Mail;

/// <summary>
/// Sends over the configured SMTP server. One connection per message — SmtpClient is not
/// thread-safe and Bugler's volume is a handful of messages per incident, so pooling would buy
/// nothing.
/// </summary>
internal sealed class MailKitMailSender(IOptions<MailOptions> options) : IMailSender
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var smtp = settings.Smtp;
        if (!smtp.IsConfigured)
        {
            // Said plainly: this text is what a caller records and what the operator reads.
            throw new InvalidOperationException("SMTP is not configured (Mail:Smtp).");
        }

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(smtp.From));
        mime.To.Add(MailboxAddress.Parse(message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        // The deadline covers connecting, authenticating and sending together, so a server that
        // accepts the socket and then falls silent cannot hold this call open.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.SendTimeoutSeconds));

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                smtp.Host, smtp.Port, SecureSocketOptions.StartTlsWhenAvailable, deadline.Token);
            if (smtp.Username.Length > 0)
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, deadline.Token);
            }

            await client.SendAsync(mime, deadline.Token);
            await client.DisconnectAsync(quit: true, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The deadline fired rather than the caller giving up. Surfaced as an ordinary
            // failure, because to everyone upstream that is what it is — one that may work
            // next time. A cancellation escaping here would instead read as "we are shutting
            // down" and stop a background loop.
            throw new TimeoutException(
                $"The SMTP server did not answer within {settings.SendTimeoutSeconds} seconds.");
        }
    }
}
