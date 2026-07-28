namespace Bugler.Ingestion.Storage;

/// <summary>
/// One log Signal normalized for storage, stamped with its authenticated Source.
/// The sender's Declared Identity is not lifted out — it stays in <see cref="ResourceAttributes"/>,
/// because the Service is what the API key proves (ADR 0006).
/// </summary>
public sealed record LogRecordRow(
    Guid ServiceId,
    DateTime Timestamp,
    DateTime? ObservedTimestamp,
    short SeverityNumber,
    string? SeverityText,
    string? Body,
    string? TraceId,
    string? SpanId,
    string? ScopeName,
    string ResourceAttributes,
    string Attributes);
