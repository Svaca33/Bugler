using Bugler.Ingestion.OtlpMapping;
using Bugler.Ingestion.Storage;
using Bugler.Registry.Contracts;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Bugler.Ingestion.ReceiveOtlpTraces;

/// <summary>OTLP/gRPC trace receiver (port 4317).</summary>
internal sealed class OtlpTraceGrpcService(TelemetryBuffer buffer, IApiKeyValidator apiKeys)
    : TraceService.TraceServiceBase
{
    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request, ServerCallContext context)
    {
        var apiKey = BearerApiKey.Extract(context.RequestHeaders.GetValue("authorization"));
        var instanceId = apiKey is null
            ? null
            : await apiKeys.ValidateAsync(apiKey, context.CancellationToken);

        if (instanceId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid API key."));
        }

        var (rows, dropped) = OtlpTraceMapper.Map(request, instanceId.Value);
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
