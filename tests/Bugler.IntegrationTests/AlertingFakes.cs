using Bugler.Alerting.DeliverMessages;
using Bugler.Mail;

namespace Bugler.IntegrationTests;

/// <summary>Records what would have left over SMTP; flips to failing to exercise the backoff.</summary>
internal sealed class RecordingMailSender : IMailSender
{
    private readonly List<MailMessage> _sent = [];

    public bool Fail { get; set; }

    public IReadOnlyList<MailMessage> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("The SMTP server is down.");
        }

        lock (_sent)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Records what would have been posted to a Google Chat webhook.</summary>
internal sealed class RecordingChatSender : IChatSender
{
    private readonly List<(string WebhookUrl, ComposedAlert Alert)> _sent = [];

    public bool Fail { get; set; }

    public IReadOnlyList<(string WebhookUrl, ComposedAlert Alert)> Sent
    {
        get
        {
            lock (_sent)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(string webhookUrl, ComposedAlert alert, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("The webhook refused the message.");
        }

        lock (_sent)
        {
            _sent.Add((webhookUrl, alert));
        }

        return Task.CompletedTask;
    }
}
