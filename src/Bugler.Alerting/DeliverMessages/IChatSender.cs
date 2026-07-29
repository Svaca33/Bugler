namespace Bugler.Alerting.DeliverMessages;

/// <summary>One message into a Google Chat space. The seam integration tests swap for a recorder.</summary>
public interface IChatSender
{
    Task SendAsync(string webhookUrl, string text, CancellationToken cancellationToken);
}
