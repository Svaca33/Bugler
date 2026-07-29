namespace Bugler.Alerting.DeliverMessages;

/// <summary>One mail leaving Bugler. The seam integration tests swap for a recorder.</summary>
public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}

public sealed record MailMessage(string ToEmail, string Subject, string TextBody);
