namespace Bugler.Ingestion.Storage;

/// <summary>One log Signal normalized for storage, stamped with its authenticated Source.</summary>
public sealed record LogRecordRow(
    Guid InstanceId,
    DateTime Timestamp,
    DateTime? ObservedTimestamp,
    short SeverityNumber,
    string? SeverityText,
    string? Body,
    string? TraceId,
    string? SpanId,
    string? ServiceName,
    string? ScopeName,
    string ResourceAttributes,
    string Attributes);
