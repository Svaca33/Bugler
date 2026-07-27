namespace Bugler.Ingestion.ReceiveOtlpLogs;

internal static class IngestionHeaders
{
    /// <summary>Header carrying the Instance's API key (set via OTEL_EXPORTER_OTLP_HEADERS).</summary>
    public const string ApiKey = "x-api-key";
}
