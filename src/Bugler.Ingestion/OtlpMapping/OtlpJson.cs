using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;

namespace Bugler.Ingestion.OtlpMapping;

/// <summary>Shared OTLP-to-storage conversions used by the log and trace mappers.</summary>
internal static class OtlpJson
{
    public static DateTime? ToUtc(ulong unixNano) =>
        unixNano == 0 ? null : DateTime.UnixEpoch.AddTicks((long)(unixNano / 100));

    public static string? ToHex(ByteString bytes, int expectedLength) =>
        bytes.Length == expectedLength ? Convert.ToHexStringLower(bytes.Span) : null;

    public static string AttributesToJson(RepeatedField<KeyValue>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return "{}";
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteAttributes(writer, attributes);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string ValueToJson(AnyValue value)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteAnyValue(writer, value);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static void WriteAttributes(Utf8JsonWriter writer, RepeatedField<KeyValue>? attributes)
    {
        writer.WriteStartObject();
        if (attributes is not null)
        {
            foreach (var attribute in attributes)
            {
                writer.WritePropertyName(StorableText.Tame(attribute.Key));
                WriteAnyValue(writer, attribute.Value);
            }
        }

        writer.WriteEndObject();
    }

    public static void WriteAnyValue(Utf8JsonWriter writer, AnyValue? value)
    {
        switch (value?.ValueCase)
        {
            case AnyValue.ValueOneofCase.StringValue:
                writer.WriteStringValue(StorableText.Tame(value.StringValue));
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
                    writer.WritePropertyName(StorableText.Tame(entry.Key));
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

    public static string? FindString(RepeatedField<KeyValue>? attributes, string key)
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
}
