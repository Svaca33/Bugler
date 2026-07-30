namespace Bugler.Mail;

/// <summary>
/// The hand-off for mail nobody waits for: the message is taken into memory and sent from a
/// background loop. Acceptance is not delivery — a queued message can still fail to leave, and
/// nothing queued survives a restart. Callers who need to know the outcome, or who keep a durable
/// record of what they owe, use <see cref="IMailSender"/> directly.
/// </summary>
public interface IMailQueue
{
    /// <summary>
    /// False when the queue is full, and then the message is never sent — the caller decides
    /// whether that is worth telling anyone about.
    /// </summary>
    bool TryEnqueue(MailMessage message);
}
