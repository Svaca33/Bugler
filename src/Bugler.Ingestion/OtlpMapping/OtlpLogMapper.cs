using System.Text;
using System.Text.Json;
using Bugler.Ingestion.Storage;
using Bugler.SharedKernel;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;

namespace Bugler.Ingestion.OtlpMapping;

/// <summary>Flattens an OTLP Export Request into storage rows stamped with the Source's instance.</summary>
public static class OtlpLogMapper
{
    public static List<LogRecordRow> Map(ExportLogsServiceRequest request, InstanceId instanceId)
    {
        var rows = new List<LogRecordRow>();
        var receivedAt = DateTime.UtcNow;

        foreach (var resourceLogs in request.ResourceLogs)
        {
            var resourceAttributes = resourceLogs.Resource?.Attributes;
            var resourceJson = AttributesToJson(resourceAttributes);
            var serviceName = FindString(resourceAttributes, "service.name");

            foreach (var scopeLogs in resourceLogs.ScopeLogs)
            {
                var scopeName = string.IsNullOrEmpty(scopeLogs.Scope?.Name) ? null : scopeLogs.Scope.Name;

                foreach (var log in scopeLogs.LogRecords)
                {
                    var observed = ToUtc(log.ObservedTimeUnixNano);
                    var timestamp = ToUtc(log.TimeUnixNano) ?? observed ?? receivedAt;

                    rows.Add(new LogRecordRow(
                        instanceId.Value,
                        timestamp,
                        observed,
                        (short)log.SeverityNumber,
                        string.IsNullOrEmpty(log.SeverityText) ? null : log.SeverityText,
                        BodyToString(log.Body),
                        ToHex(log.TraceId, expectedLength: 16),
                        ToHex(log.SpanId, expectedLength: 8),
                        serviceName,
                        scopeName,
                        resourceJson,
                        AttributesToJson(log.Attributes)));
                }
            }
        }

        return rows;
    }

    private static DateTime? ToUtc(ulong unixNano) =>
        unixNano == 0 ? null : DateTime.UnixEpoch.AddTicks((long)(unixNano / 100));

    private static string? ToHex(ByteString bytes, int expectedLength) =>
        bytes.Length == expectedLength ? Convert.ToHexStringLower(bytes.Span) : null;

    private static string? FindString(RepeatedField<KeyValue>? attributes, string key)
    {
        if (attributes is null)
        {
            return null;
        }

        foreach (var attribute in attributes)
        {
            if (attribute.Key == key && attribute.Value?.ValueCase == AnyValue.ValueOneofCase.StringValue)
            {
                return attribute.Value.StringValue;
            }
        }

        return null;
    }

    private static string? BodyToString(AnyValue? body) => body?.ValueCase switch
    {
        null or AnyValue.ValueOneofCase.None => null,
        AnyValue.ValueOneofCase.StringValue => body.StringValue,
        _ => ToJson(body),
    };

    private static string AttributesToJson(RepeatedField<KeyValue>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return "{}";
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var attribute in attributes)
            {
                writer.WritePropertyName(attribute.Key);
                WriteAnyValue(writer, attribute.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string ToJson(AnyValue value)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteAnyValue(writer, value);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteAnyValue(Utf8JsonWriter writer, AnyValue? value)
    {
        switch (value?.ValueCase)
        {
            case AnyValue.ValueOneofCase.StringValue:
                writer.WriteStringValue(value.StringValue);
                break;
            case AnyValue.ValueOneofCase.BoolValue:
                writer.WriteBooleanValue(value.BoolValue);
                break;
            case AnyValue.ValueOneofCase.IntValue:
                writer.WriteNumberValue(value.IntValue);
                break;
            case AnyValue.ValueOneofCase.DoubleValue:
                writer.WriteNumberValue(value.DoubleValue);
                break;
            case AnyValue.ValueOneofCase.ArrayValue:
                writer.WriteStartArray();
                foreach (var item in value.ArrayValue.Values)
                {
                    WriteAnyValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case AnyValue.ValueOneofCase.KvlistValue:
                writer.WriteStartObject();
                foreach (var entry in value.KvlistValue.Values)
                {
                    writer.WritePropertyName(entry.Key);
                    WriteAnyValue(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case AnyValue.ValueOneofCase.BytesValue:
                writer.WriteStringValue(value.BytesValue.ToBase64());
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
