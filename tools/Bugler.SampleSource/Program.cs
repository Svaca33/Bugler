// Sample telemetry Source for manual testing: simulates a small e-shop and streams
// its traces + correlated logs into a running Bugler over OTLP. See README.md.

using Bugler.SampleSource;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

SampleSourceOptions? options;
try
{
    options = SampleSourceOptions.Parse(args);
}
catch (OptionsError error)
{
    return SampleSourceOptions.PrintError(error);
}

if (options is null)
{
    return 0;
}

using var diagnostics = new OtelDiagnosticsListener();

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(options.ServiceName, serviceInstanceId: Environment.MachineName)
    .AddAttributes([KeyValuePair.Create<string, object>("deployment.environment", "sample")]);

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(ShopSimulation.ActivitySourceName)
    .AddOtlpExporter(exporter => ConfigureOtlp(exporter, "v1/traces"))
    .Build();

var loggerFactory = LoggerFactory.Create(logging => logging
    .SetMinimumLevel(LogLevel.Information)
    .AddOpenTelemetry(otel =>
    {
        otel.SetResourceBuilder(resourceBuilder);
        otel.IncludeFormattedMessage = true;
        otel.AddOtlpExporter(exporter => ConfigureOtlp(exporter, "v1/logs"));
    }));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Streaming sample e-shop telemetry as service '{options.ServiceName}'");
Console.WriteLine($"  endpoint: {options.Endpoint} ({(options.Protocol == OtlpExportProtocol.Grpc ? "grpc" : "http")})");
Console.WriteLine($"  rate:     {options.Rate:0.##} ops/s, {(options.Count > 0 ? $"{options.Count} operations" : "until Ctrl+C")}");
Console.WriteLine();

var simulation = new ShopSimulation(loggerFactory);
var sent = 0;
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / options.Rate));
try
{
    while (options.Count == 0 || sent < options.Count)
    {
        var operation = await simulation.RunOperationAsync(cts.Token);
        sent++;
        if (!options.Quiet)
        {
            PrintOperation(sent, operation);
        }

        if (options.Count > 0 && sent >= options.Count)
        {
            break;
        }

        await timer.WaitForNextTickAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
    // Ctrl+C — fall through to flush what is still buffered.
}

Console.WriteLine();
Console.WriteLine("Flushing telemetry…");
loggerFactory.Dispose();
tracerProvider?.ForceFlush(10_000);
tracerProvider?.Dispose();
Console.WriteLine($"Sent {sent} operations to {options.Endpoint} as '{options.ServiceName}'.");
return 0;

void ConfigureOtlp(OtlpExporterOptions exporter, string signalPath)
{
    // For OTLP/HTTP the exporter uses the endpoint as-is, so it must carry the
    // signal path; for gRPC the service is addressed by the protocol itself.
    exporter.Protocol = options.Protocol;
    exporter.Endpoint = options.Protocol == OtlpExportProtocol.Grpc
        ? options.Endpoint
        : new Uri(options.Endpoint, signalPath);
    exporter.Headers = $"Authorization=Bearer {options.ApiKey}";
}

static void PrintOperation(int n, OperationResult operation)
{
    var (label, color) = operation.Outcome switch
    {
        Outcome.Ok => ("ok   ", ConsoleColor.DarkGreen),
        Outcome.Warning => ("warn ", ConsoleColor.DarkYellow),
        _ => ("error", ConsoleColor.Red),
    };

    Console.Write($"{DateTime.Now:HH:mm:ss} #{n,-5} ");
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(label);
    Console.ForegroundColor = previous;
    Console.WriteLine($" {operation.Name,-24} {operation.Detail,-42} trace={operation.TraceId}");
}
