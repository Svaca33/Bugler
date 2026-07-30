using Bugler.Mail;

namespace Bugler.IntegrationTests;

/// <summary>
/// Takes the place of the background mail queue: what a request hands over is readable at once,
/// with no loop to wait for.
/// </summary>
internal sealed class RecordingMailQueue : IMailQueue
{
    private readonly List<MailMessage> _queued = [];

    /// <summary>Set to make the queue behave as a full one.</summary>
    public bool Full { get; set; }

    public IReadOnlyList<MailMessage> Queued
    {
        get
        {
            lock (_queued)
            {
                return _queued.ToList();
            }
        }
    }

    public bool TryEnqueue(MailMessage message)
    {
        if (Full)
        {
            return false;
        }

        lock (_queued)
        {
            _queued.Add(message);
        }

        return true;
    }

    /// <summary>The secret as the person who opens the mail would use it — out of the link itself.</summary>
    public static string TokenIn(MailMessage message)
    {
        var marker = message.TextBody.IndexOf("?token=", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"No reset link in the mail:\n{message.TextBody}");

        var token = message.TextBody[(marker + "?token=".Length)..];
        var end = token.IndexOfAny(['\n', '\r', ' ']);
        return Uri.UnescapeDataString(end < 0 ? token : token[..end]);
    }
}
