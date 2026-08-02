using System.Text;
using System.Text.Json;
using Bugler.Ingestion.ObserveReleases;
using Bugler.Ingestion.Storage;
using Bugler.SharedKernel;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.Ingestion.OtlpMapping;

/// <summary>
/// Flattens an OTLP trace Export Request into storage rows stamped with the Source's Service.
/// The request's Declared Identity is left inside the resource attributes — it never
/// competes with the Service the API key proved (ADR 0006).
/// </summary>
public static class OtlpTraceMapper
{
    /// <returns>
    /// The mapped rows, the count of spans dropped for malformed trace/span ids, and what each
    /// Resource block declared its version to be. Traces observe Releases exactly as logs do — a
    /// Service that only sends spans is no less deployed (ADR 0016).
    /// </returns>
    public static (List<SpanRow> Rows, int Dropped, List<DeclaredVersionSighting> Versions) Map(
        ExportTraceServiceRequest request, ServiceId serviceId)
    {
        var rows = new List<SpanRow>();
        var versions = new List<DeclaredVersionSighting>();
        var dropped = 0;
        var receivedAt = DateTime.UtcNow;

        foreach (var resourceSpans in request.ResourceSpans)
        {
            var resourceJson = OtlpJson.AttributesToJson(resourceSpans.Resource?.Attributes);
            var declaredVersion = DeclaredVersion.Read(resourceSpans.Resource?.Attributes);
            DateTime? earliest = null;

            foreach (var scopeSpans in resourceSpans.ScopeSpans)
            {
                var scopeName = string.IsNullOrEmpty(scopeSpans.Scope?.Name)
                    ? null
                    : StorableText.Tame(scopeSpans.Scope.Name);

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

                    if (earliest is null || startTime < earliest)
                    {
                        earliest = startTime;
                    }

                    rows.Add(new SpanRow(
                        serviceId.Value,
                        traceId,
                        spanId,
                        OtlpJson.ToHex(span.ParentSpanId, expectedLength: 8),
                        StorableText.Tame(span.Name),
                        (short)span.Kind,
                        startTime,
                        OtlpJson.ToUtc(span.EndTimeUnixNano) ?? startTime,
                        (short)(span.Status?.Code ?? Status.Types.StatusCode.Unset),
                        string.IsNullOrEmpty(span.Status?.Message) ? null : StorableText.Tame(span.Status.Message),
                        scopeName,
                        resourceJson,
                        OtlpJson.AttributesToJson(span.Attributes),
                        EventsToJson(span),
                        LinksToJson(span)));
                }
            }

            // A block declaring a version but carrying no usable span has nothing to be dated by,
            // and a Release with no instant is not one.
            if (declaredVersion is not null && earliest is { } earliestSignal)
            {
                versions.Add(new DeclaredVersionSighting(declaredVersion, earliestSignal));
            }
        }

        return (rows, dropped, versions);
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
                writer.WriteString("name", StorableText.Tame(spanEvent.Name));
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
