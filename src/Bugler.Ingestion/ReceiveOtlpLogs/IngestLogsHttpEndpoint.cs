using Bugler.Ingestion.ObserveReleases;
using Bugler.Ingestion.OtlpMapping;
using Bugler.Ingestion.Storage;
using Bugler.Registry.Contracts;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Bugler.Ingestion.ReceiveOtlpLogs;

/// <summary>OTLP/HTTP log receiver — POST /v1/logs with a protobuf body (port 4318).</summary>
internal static class IngestLogsHttpEndpoint
{
    private const string ProtobufContentType = "application/x-protobuf";

    public static async Task<IResult> Handle(
        HttpRequest request,
        TelemetryBuffer buffer,
        ReleaseObservations releases,
        IApiKeyValidator apiKeys,
        CancellationToken cancellationToken)
    {
        var apiKey = BearerApiKey.Extract(request.Headers.Authorization.FirstOrDefault());
        var serviceId = apiKey is null
            ? null
            : await apiKeys.ValidateAsync(apiKey, cancellationToken);

        if (serviceId is null)
        {
            return Results.Unauthorized();
        }

        if (!IsProtobuf(request.ContentType))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        ExportLogsServiceRequest export;
        try
        {
            using var body = new MemoryStream();
            await request.Body.CopyToAsync(body, cancellationToken);
            body.Position = 0;
            export = ExportLogsServiceRequest.Parser.ParseFrom(body);
        }
        catch (InvalidProtocolBufferException)
        {
            return Results.BadRequest();
        }

        var (rows, versions) = OtlpLogMapper.Map(export, serviceId.Value);
        // Before the buffer, and unaffected by it: the export arrived and declared that version,
        // so the Release stands even where a full buffer turns its Signals away (ADR 0016).
        releases.Observe(serviceId.Value, versions);
        var rejected = rows.Count(row => !buffer.TryEnqueue(row));

        if (rejected == rows.Count && rows.Count > 0)
        {
            return Results.Text("Ingest buffer is full; retry later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var response = new ExportLogsServiceResponse();
        if (rejected > 0)
        {
            response.PartialSuccess = new ExportLogsPartialSuccess
            {
                RejectedLogRecords = rejected,
                ErrorMessage = "Ingest buffer is full; rejected records may be retried.",
            };
        }

        return Results.Bytes(response.ToByteArray(), ProtobufContentType);
    }

    private static bool IsProtobuf(string? contentType) =>
        contentType is not null &&
        contentType.StartsWith(ProtobufContentType, StringComparison.OrdinalIgnoreCase);
}
