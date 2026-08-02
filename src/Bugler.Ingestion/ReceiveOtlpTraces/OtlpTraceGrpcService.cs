using Bugler.Ingestion.ObserveReleases;
using Bugler.Ingestion.OtlpMapping;
using Bugler.Ingestion.Storage;
using Bugler.Registry.Contracts;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Bugler.Ingestion.ReceiveOtlpTraces;

/// <summary>OTLP/gRPC trace receiver (port 4317).</summary>
internal sealed class OtlpTraceGrpcService(
    TelemetryBuffer buffer, ReleaseObservations releases, IApiKeyValidator apiKeys)
    : TraceService.TraceServiceBase
{
    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request, ServerCallContext context)
    {
        var apiKey = BearerApiKey.Extract(context.RequestHeaders.GetValue("authorization"));
        var serviceId = apiKey is null
            ? null
            : await apiKeys.ValidateAsync(apiKey, context.CancellationToken);

        if (serviceId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid API key."));
        }

        var (rows, dropped, versions) = OtlpTraceMapper.Map(request, serviceId.Value);
        // Before the buffer, and unaffected by it: the export arrived and declared that version,
        // so the Release stands even where a full buffer turns its Signals away (ADR 0016).
        releases.Observe(serviceId.Value, versions);
        var overflow = rows.Count(row => !buffer.TryEnqueue(row));

        if (overflow == rows.Count && rows.Count > 0)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Ingest buffer is full; retry later."));
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

        return response;
    }
}
