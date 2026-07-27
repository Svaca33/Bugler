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

    public TelemetryBuffer(IOptions<IngestionOptions> options)
    {
        _logs = Channel.CreateBounded<LogRecordRow>(new BoundedChannelOptions(options.Value.BufferCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite,
        });
    }

    public bool TryEnqueue(LogRecordRow row) => _logs.Writer.TryWrite(row);

    public ChannelReader<LogRecordRow> Logs => _logs.Reader;
}
