using Bugler.Ingestion.Storage;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;

namespace Bugler.Ingestion.ObserveReleases;

/// <summary>
/// Reads the Declared Version out of an OTLP Resource. Taken from the structured attributes while
/// they are still a protobuf message — once they are serialized into the jsonb column, finding it
/// again costs a scan of every stored row.
/// </summary>
internal static class DeclaredVersion
{
    /// <summary>The OTel Resource Attribute a sender states its version in — stable and recommended.</summary>
    private const string AttributeKey = "service.version";

    /// <summary>
    /// The Declared Version, or null where there is none to have. A version is only ever a string:
    /// semconv leaves its format open — `2.0.0` and `a01dbef8a` are both valid — but not its type,
    /// so a number or a nested object is a sender getting it wrong, not a version. Blank and
    /// over-long values are read as absent too, and absent means nothing is observed at all.
    /// </summary>
    public static string? Read(RepeatedField<KeyValue>? attributes)
    {
        if (attributes is null)
        {
            return null;
        }

        foreach (var attribute in attributes)
        {
            if (attribute.Key != AttributeKey)
            {
                continue;
            }

            if (attribute.Value?.ValueCase != AnyValue.ValueOneofCase.StringValue)
            {
                return null;
            }

            var version = attribute.Value.StringValue.Trim();
            return version.Length is 0 or > IngestionDbContext.DeclaredVersionLimit ? null : version;
        }

        return null;
    }
}
