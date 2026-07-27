using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Bugler.Ingestion.Storage;

/// <summary>
/// The bounded in-memory buffer between receiving an Export Request and persisting
/// its Signals (ADR 0003). A full buffer is a Rejection, never a silent drop.
/// </summary>
internal sealed class TelemetryBuffer
{
    private readonly Channel<LogRecordRow> _logs;
    private readonly Channel<SpanRow> _spans;

    public TelemetryBuffer(IOptions<IngestionOptions> options)
    {
        var channelOptions = new BoundedChannelOptions(options.Value.BufferCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite,
        };
        _logs = Channel.CreateBounded<LogRecordRow>(channelOptions);
        _spans = Channel.CreateBounded<SpanRow>(channelOptions);
    }

    public bool TryEnqueue(LogRecordRow row) => _logs.Writer.TryWrite(row);

    public bool TryEnqueue(SpanRow row) => _spans.Writer.TryWrite(row);

    public ChannelReader<LogRecordRow> Logs => _logs.Reader;

    public ChannelReader<SpanRow> Spans => _spans.Reader;
}
