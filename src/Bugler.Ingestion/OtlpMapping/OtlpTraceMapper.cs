using System.Text;
using System.Text.Json;
using Bugler.Ingestion.Storage;
using Bugler.SharedKernel;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.Ingestion.OtlpMapping;

/// <summary>Flattens an OTLP trace Export Request into storage rows stamped with the Source's instance.</summary>
public static class OtlpTraceMapper
{
    /// <returns>The mapped rows plus the count of spans dropped for malformed trace/span ids.</returns>
    public static (List<SpanRow> Rows, int Dropped) Map(ExportTraceServiceRequest request, InstanceId instanceId)
    {
        var rows = new List<SpanRow>();
        var dropped = 0;
        var receivedAt = DateTime.UtcNow;

        foreach (var resourceSpans in request.ResourceSpans)
        {
            var resourceAttributes = resourceSpans.Resource?.Attributes;
            var resourceJson = OtlpJson.AttributesToJson(resourceAttributes);
            var serviceName = OtlpJson.FindString(resourceAttributes, "service.name");

            foreach (var scopeSpans in resourceSpans.ScopeSpans)
            {
                var scopeName = string.IsNullOrEmpty(scopeSpans.Scope?.Name) ? null : scopeSpans.Scope.Name;

                foreach (var span in scopeSpans.Spans)
                {
                    var traceId = OtlpJson.ToHex(span.TraceId, expectedLength: 16);
                    var spanId = OtlpJson.ToHex(span.SpanId, expectedLength: 8);
                    if (traceId is null || spanId is null)
                    {
                        dropped++;
                        continue;
                    }

                    var startTime = OtlpJson.ToUtc(span.StartTimeUnixNano) ?? receivedAt;

                    rows.Add(new SpanRow(
                        instanceId.Value,
                        traceId,
                        spanId,
                        OtlpJson.ToHex(span.ParentSpanId, expectedLength: 8),
                        span.Name,
                        (short)span.Kind,
                        startTime,
                        OtlpJson.ToUtc(span.EndTimeUnixNano) ?? startTime,
                        (short)(span.Status?.Code ?? Status.Types.StatusCode.Unset),
                        string.IsNullOrEmpty(span.Status?.Message) ? null : span.Status.Message,
                        serviceName,
                        scopeName,
                        resourceJson,
                        OtlpJson.AttributesToJson(span.Attributes),
                        EventsToJson(span),
                        LinksToJson(span)));
                }
            }
        }

        return (rows, dropped);
    }

    private static string EventsToJson(Span span)
    {
        if (span.Events.Count == 0)
        {
            return "[]";
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var spanEvent in span.Events)
            {
                writer.WriteStartObject();
                writer.WriteString("name", spanEvent.Name);
                if (OtlpJson.ToUtc(spanEvent.TimeUnixNano) is { } time)
                {
                    writer.WriteString("time", time);
                }

                writer.WritePropertyName("attributes");
                OtlpJson.WriteAttributes(writer, spanEvent.Attributes);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string LinksToJson(Span span)
    {
        if (span.Links.Count == 0)
        {
            return "[]";
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var link in span.Links)
            {
                writer.WriteStartObject();
                writer.WriteString("traceId", OtlpJson.ToHex(link.TraceId, expectedLength: 16));
                writer.WriteString("spanId", OtlpJson.ToHex(link.SpanId, expectedLength: 8));
                writer.WritePropertyName("attributes");
                OtlpJson.WriteAttributes(writer, link.Attributes);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
