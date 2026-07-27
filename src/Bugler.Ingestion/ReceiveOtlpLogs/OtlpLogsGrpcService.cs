using Bugler.Ingestion.OtlpMapping;
using Bugler.Ingestion.Storage;
using Bugler.Registry.Contracts;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;

namespace Bugler.Ingestion.ReceiveOtlpLogs;

/// <summary>OTLP/gRPC log receiver (port 4317).</summary>
internal sealed class OtlpLogsGrpcService(TelemetryBuffer buffer, IApiKeyValidator apiKeys)
    : LogsService.LogsServiceBase
{
    public override async Task<ExportLogsServiceResponse> Export(
        ExportLogsServiceRequest request, ServerCallContext context)
    {
        var apiKey = BearerApiKey.Extract(context.RequestHeaders.GetValue("authorization"));
        var instanceId = apiKey is null
            ? null
            : await apiKeys.ValidateAsync(apiKey, context.CancellationToken);

        if (instanceId is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid API key."));
        }

        var rows = OtlpLogMapper.Map(request, instanceId.Value);
        var rejected = rows.Count(row => !buffer.TryEnqueue(row));

        if (rejected == rows.Count && rows.Count > 0)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Ingest buffer is full; retry later."));
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

        return response;
    }
}
