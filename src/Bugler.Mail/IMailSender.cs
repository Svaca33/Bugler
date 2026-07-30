namespace Bugler.Mail;

/// <summary>
/// One mail leaving Bugler, awaited to its outcome: it returns once the SMTP server has accepted
/// the message and throws when it has not. For callers that keep their own record of what is owed
/// and pursue it themselves. Whoever must not keep a request waiting hands the message to
/// <see cref="IMailQueue"/> instead. The integration tests swap this for a recorder.
/// </summary>
public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}

public sealed record MailMessage(string ToEmail, string Subject, string TextBody);
