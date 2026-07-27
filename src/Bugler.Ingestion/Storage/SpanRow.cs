namespace Bugler.Ingestion.Storage;

/// <summary>One span Signal normalized for storage, stamped with its authenticated Source.</summary>
public sealed record SpanRow(
    Guid InstanceId,
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    short Kind,
    DateTime StartTime,
    DateTime EndTime,
    short StatusCode,
    string? StatusMessage,
    string? ServiceName,
    string? ScopeName,
    string ResourceAttributes,
    string Attributes,
    string Events,
    string Links);
