using Bugler.Ingestion.Storage;
using Bugler.SharedKernel;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;

namespace Bugler.Ingestion.OtlpMapping;

/// <summary>
/// Flattens an OTLP Export Request into storage rows stamped with the Source's Service.
/// The request's Declared Identity is left inside the resource attributes — it never
/// competes with the Service the API key proved (ADR 0006).
/// </summary>
public static class OtlpLogMapper
{
    public static List<LogRecordRow> Map(ExportLogsServiceRequest request, ServiceId serviceId)
    {
        var rows = new List<LogRecordRow>();
        var receivedAt = DateTime.UtcNow;

        foreach (var resourceLogs in request.ResourceLogs)
        {
            var resourceJson = OtlpJson.AttributesToJson(resourceLogs.Resource?.Attributes);

            foreach (var scopeLogs in resourceLogs.ScopeLogs)
            {
                var scopeName = string.IsNullOrEmpty(scopeLogs.Scope?.Name) ? null : scopeLogs.Scope.Name;

                foreach (var log in scopeLogs.LogRecords)
                {
                    var observed = OtlpJson.ToUtc(log.ObservedTimeUnixNano);
                    var timestamp = OtlpJson.ToUtc(log.TimeUnixNano) ?? observed ?? receivedAt;

                    rows.Add(new LogRecordRow(
                        serviceId.Value,
                        timestamp,
                        observed,
                        (short)log.SeverityNumber,
                        string.IsNullOrEmpty(log.SeverityText) ? null : log.SeverityText,
                        BodyToString(log.Body),
                        OtlpJson.ToHex(log.TraceId, expectedLength: 16),
                        OtlpJson.ToHex(log.SpanId, expectedLength: 8),
                        scopeName,
                        resourceJson,
                        OtlpJson.AttributesToJson(log.Attributes)));
                }
            }
        }

        return rows;
    }

    private static string? BodyToString(AnyValue? body) => body?.ValueCase switch
    {
        null or AnyValue.ValueOneofCase.None => null,
        AnyValue.ValueOneofCase.StringValue => body.StringValue,
        _ => OtlpJson.ValueToJson(body),
    };
}
