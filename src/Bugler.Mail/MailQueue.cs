using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Bugler.Mail;

/// <summary>
/// The bounded in-memory hand-off between a request and the loop that sends. Bounded on purpose:
/// a full queue drops the message and says so, rather than letting requests pile up behind a mail
/// server that stopped answering.
/// </summary>
internal sealed class MailQueue : IMailQueue
{
    private readonly Channel<MailMessage> _messages;

    public MailQueue(IOptions<MailOptions> options) =>
        _messages = Channel.CreateBounded<MailMessage>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropWrite,
            });

    public bool TryEnqueue(MailMessage message) => _messages.Writer.TryWrite(message);

    public ChannelReader<MailMessage> Messages => _messages.Reader;
}
