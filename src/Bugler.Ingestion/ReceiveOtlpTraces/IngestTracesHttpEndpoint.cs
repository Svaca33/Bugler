using Bugler.Ingestion.OtlpMapping;
using Bugler.Ingestion.Storage;
using Bugler.Registry.Contracts;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Bugler.Ingestion.ReceiveOtlpTraces;

/// <summary>OTLP/HTTP trace receiver — POST /v1/traces with a protobuf body (port 4318).</summary>
internal static class IngestTracesHttpEndpoint
{
    private const string ProtobufContentType = "application/x-protobuf";

    public static async Task<IResult> Handle(
        HttpRequest request,
        TelemetryBuffer buffer,
        IApiKeyValidator apiKeys,
        CancellationToken cancellationToken)
    {
        var apiKey = BearerApiKey.Extract(request.Headers.Authorization.FirstOrDefault());
        var instanceId = apiKey is null
            ? null
            : await apiKeys.ValidateAsync(apiKey, cancellationToken);

        if (instanceId is null)
        {
            return Results.Unauthorized();
        }

        if (!IsProtobuf(request.ContentType))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        ExportTraceServiceRequest export;
        try
        {
            using var body = new MemoryStream();
            await request.Body.CopyToAsync(body, cancellationToken);
            body.Position = 0;
            export = ExportTraceServiceRequest.Parser.ParseFrom(body);
        }
        catch (InvalidProtocolBufferException)
        {
            return Results.BadRequest();
        }

        var (rows, dropped) = OtlpTraceMapper.Map(export, instanceId.Value);
        var overflow = rows.Count(row => !buffer.TryEnqueue(row));

        if (overflow == rows.Count && rows.Count > 0)
        {
            return Results.Text("Ingest buffer is full; retry later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var response = new ExportTraceServiceResponse();
        if (dropped + overflow > 0)
        {
            response.PartialSuccess = new ExportTracePartialSuccess
            {
                RejectedSpans = dropped + overflow,
                ErrorMessage = "Some spans were rejected (malformed ids or full buffer).",
            };
        }

        return Results.Bytes(response.ToByteArray(), ProtobufContentType);
    }

    private static bool IsProtobuf(string? contentType) =>
        contentType is not null &&
        contentType.StartsWith(ProtobufContentType, StringComparison.OrdinalIgnoreCase);
}
