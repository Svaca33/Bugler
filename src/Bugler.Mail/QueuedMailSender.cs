using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Mail;

/// <summary>
/// Drains the queue and sends what it finds, one message at a time. A failed send is retried a
/// couple of times and then given up on and logged: the queue keeps no record of what it was
/// carrying, so there is nothing left to pursue once the attempts run out — and nothing to resume
/// after a restart. Whoever queued the message can be asked for it again.
/// </summary>
internal sealed class QueuedMailSender(
    MailQueue queue,
    IMailSender sender,
    IOptions<MailOptions> options,
    ILogger<QueuedMailSender> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Smtp.IsConfigured)
        {
            // The one place that says it, for every context that would have sent something.
            logger.LogWarning(
                "SMTP is not configured (Mail:Smtp): no mail will leave Bugler — alerts reach "
                + "their subscribers only in the UI and password resets stay unavailable");
        }

        try
        {
            while (await queue.Messages.WaitToReadAsync(stoppingToken))
            {
                while (queue.Messages.TryRead(out var message))
                {
                    await SendWithRetriesAsync(message, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Whatever is still queued is lost by design.
        }
    }

    private async Task SendWithRetriesAsync(MailMessage message, CancellationToken stoppingToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Never the recipient: this log lands in Bugler itself, where anyone with an
                // Application Grant would read it.
                if (attempt > RetryDelays.Length)
                {
                    logger.LogError(
                        exception,
                        "Gave up sending a mail ({Subject}) after {Attempts} attempts",
                        message.Subject, attempt);
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Sending a mail ({Subject}) failed on attempt {Attempt}; retrying",
                    message.Subject, attempt);
            }

            try
            {
                await Task.Delay(RetryDelays[attempt - 1], stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
