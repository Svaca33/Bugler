using System.Diagnostics.Tracing;
using System.Globalization;

namespace Bugler.SampleSource;

/// <summary>
/// Surfaces OpenTelemetry SDK warnings (export failures, dropped batches) on stderr —
/// the SDK only reports them through its EventSource, so a wrong endpoint or a
/// rejected API key would otherwise fail silently.
/// </summary>
internal sealed class OtelDiagnosticsListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith("OpenTelemetry-", StringComparison.Ordinal))
        {
            EnableEvents(eventSource, EventLevel.Warning);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        string message;
        try
        {
            message = e.Message is null
                ? string.Join(" ", e.Payload ?? [])
                : string.Format(CultureInfo.InvariantCulture, e.Message, e.Payload?.ToArray() ?? []);
        }
        catch (FormatException)
        {
            message = e.Message ?? e.EventName ?? "unknown event";
        }

        Console.Error.WriteLine($"[otel {e.Level}] {message}");
    }
}
