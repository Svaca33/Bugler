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

var declaredVersion = options.DeclaredVersion;

// Rolling only means something while a version is declared at all: `--version ""` is the sender
// that states none, and there is nothing there to raise.
var rollEvery = options.VersionEveryMinutes > 0 && declaredVersion is not null
    ? TimeSpan.FromMinutes(options.VersionEveryMinutes)
    : (TimeSpan?)null;

TracerProvider? tracerProvider = null;
ILoggerFactory loggerFactory = null!;
BuildTelemetry();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("Streaming sample e-shop telemetry into Bugler");
Console.WriteLine($"  endpoint: {options.Endpoint} ({(options.Protocol == OtlpExportProtocol.Grpc ? "grpc" : "http")})");
Console.WriteLine($"  rate:     {options.Rate:0.##} ops/s, {(options.Count > 0 ? $"{options.Count} operations" : "until Ctrl+C")}");
Console.WriteLine($"  errors:   {options.DeclineRate} % of checkouts decline, "
    + (options.RareErrorMinutes > 0
        ? $"inventory sync fails every {options.RareErrorMinutes:0.##}+ min"
        : "inventory sync failures off"));
Console.WriteLine($"  declared: {options.DeclaredIdentity}, replica {options.Replica}");
Console.WriteLine("            (filed under whichever Service the API key belongs to)");
Console.WriteLine($"  version:  {declaredVersion ?? "none declared"}"
    + (rollEvery is { } every ? $", raised every {every.TotalMinutes:0.##} min" : ", fixed"));
Console.WriteLine();

// The simulation outlives the providers: it is handed the current factory rather than one of them,
// so a version roll does not restart the clock its rare error is spaced on.
var simulation = new ShopSimulation(
    () => loggerFactory,
    options.DeclineRate,
    options.RareErrorMinutes > 0 ? TimeSpan.FromMinutes(options.RareErrorMinutes) : null);
var sent = 0;
var raiseAt = rollEvery is { } interval ? DateTime.UtcNow + interval : DateTime.MaxValue;
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

        // Between operations, never inside one: a Signal must not be split across two Resources.
        if (DateTime.UtcNow >= raiseAt)
        {
            RaiseDeclaredVersion();
            raiseAt = DateTime.UtcNow + rollEvery!.Value;
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
RetireTelemetry();
Console.WriteLine($"Sent {sent} operations to {options.Endpoint} declared as '{options.DeclaredIdentity}'"
    + (declaredVersion is null ? "." : $", last version '{declaredVersion}'."));
return 0;

/// <summary>
/// Builds the providers for the version currently declared. A Resource is fixed when a provider is
/// built, so declaring a new version means new providers — there is nothing on a live one to set.
/// </summary>
void BuildTelemetry()
{
    // The declared identity: the same three facets a Service is registered by, plus the
    // service.instance.id that tells its replicas apart. Bugler files everything under the
    // Service the API key proves and never reads these (ADR 0006) — they are sent so the
    // sample data mirrors its registration instead of contradicting it. service.version is the
    // exception: a change of it is observed as a Release (ADR 0016).
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(
            options.ServiceName,
            serviceNamespace: options.Namespace,
            serviceVersion: declaredVersion,
            serviceInstanceId: options.Replica)
        .AddAttributes(
            [KeyValuePair.Create<string, object>("deployment.environment.name", options.EnvironmentName)]);

    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        .AddSource(ShopSimulation.ActivitySourceName)
        .AddOtlpExporter(exporter => ConfigureOtlp(exporter, "v1/traces"))
        .Build();

    loggerFactory = LoggerFactory.Create(logging => logging
        .SetMinimumLevel(LogLevel.Information)
        .AddOpenTelemetry(otel =>
        {
            otel.SetResourceBuilder(resourceBuilder);
            otel.IncludeFormattedMessage = true;
            otel.AddOtlpExporter(exporter => ConfigureOtlp(exporter, "v1/logs"));
        }));
}

void RetireTelemetry()
{
    loggerFactory.Dispose();
    tracerProvider?.ForceFlush(10_000);
    tracerProvider?.Dispose();
}

/// <summary>
/// Retires the old version's providers before the new ones exist, which flushes everything the old
/// version sent before anything the new one sends. Nothing is in flight in between — this runs
/// between operations — so the handover is clean rather than a rolling deploy's overlap.
/// </summary>
void RaiseDeclaredVersion()
{
    var raised = SampleSourceOptions.RaiseVersion(declaredVersion!);
    RetireTelemetry();
    declaredVersion = raised;
    BuildTelemetry();
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} {"",-6} now declaring version {raised}");
}

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
