using OpenTelemetry.Exporter;

namespace Bugler.SampleSource;

internal sealed record SampleSourceOptions(
    string ApiKey,
    Uri Endpoint,
    OtlpExportProtocol Protocol,
    double Rate,
    int Count,
    string Namespace,
    string EnvironmentName,
    string ServiceName,
    string Replica,
    bool Quiet)
{
    private const string Usage = """
        Sends a continuous stream of sample e-shop telemetry (traces + correlated logs)
        to a running Bugler over OTLP.

        Usage:
          dotnet run --project tools/Bugler.SampleSource -- --api-key blgr_... [options]

        Options:
          --api-key <key>    Service API key; falls back to the BUGLER_API_KEY env var
          --protocol <p>     http | grpc (default: http)
          --endpoint <url>   OTLP endpoint (default: http://localhost:4318 for http,
                             http://localhost:4317 for grpc)
          --rate <ops/s>     target operations per second (default: 2)
          --count <n>        stop after N operations (default: run until Ctrl+C)
          --quiet            suppress per-operation output
          --help             show this help

        Declared identity — resource attributes only; the API key is what decides the
        Service the telemetry is filed under (ADR 0006). Set these to the facets of the
        Service the key belongs to so the payload does not contradict its registration:
          --namespace <ns>   service.namespace (default: demo)
          --environment <e>  deployment.environment.name (default: sample)
          --service <name>   service.name (default: sample-eshop)
          --replica <id>     service.instance.id, which tells replicas of one Service
                             apart (default: the machine name)
        """;

    public static SampleSourceOptions? Parse(string[] args)
    {
        string? apiKey = Environment.GetEnvironmentVariable("BUGLER_API_KEY");
        string? endpoint = null;
        var protocol = OtlpExportProtocol.HttpProtobuf;
        var rate = 2.0;
        var count = 0;
        var serviceNamespace = "demo";
        var environmentName = "sample";
        var serviceName = "sample-eshop";
        var replica = Environment.MachineName;
        var quiet = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    Console.WriteLine(Usage);
                    return null;
                case "--api-key":
                    apiKey = Next(args, ref i);
                    break;
                case "--endpoint":
                    endpoint = Next(args, ref i);
                    break;
                case "--protocol":
                    protocol = Next(args, ref i) switch
                    {
                        "http" => OtlpExportProtocol.HttpProtobuf,
                        "grpc" => OtlpExportProtocol.Grpc,
                        var other => throw new OptionsError($"Unknown protocol '{other}' — use http or grpc."),
                    };
                    break;
                case "--rate":
                    if (!double.TryParse(Next(args, ref i), out rate) || rate <= 0)
                    {
                        throw new OptionsError("--rate must be a positive number of operations per second.");
                    }

                    break;
                case "--count":
                    if (!int.TryParse(Next(args, ref i), out count) || count < 0)
                    {
                        throw new OptionsError("--count must be a non-negative integer.");
                    }

                    break;
                case "--namespace":
                    serviceNamespace = Next(args, ref i);
                    break;
                case "--environment":
                    environmentName = Next(args, ref i);
                    break;
                case "--service":
                    serviceName = Next(args, ref i);
                    break;
                case "--replica":
                    replica = Next(args, ref i);
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                case var unknown:
                    throw new OptionsError($"Unknown option '{unknown}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new OptionsError("An API key is required: pass --api-key or set BUGLER_API_KEY.");
        }

        endpoint ??= protocol == OtlpExportProtocol.Grpc ? "http://localhost:4317" : "http://localhost:4318";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme is not ("http" or "https"))
        {
            throw new OptionsError($"--endpoint '{endpoint}' is not an absolute http(s) URL.");
        }

        return new SampleSourceOptions(
            apiKey, endpointUri, protocol, rate, count, serviceNamespace, environmentName, serviceName, replica, quiet);
    }

    /// <summary>The identity the payload declares about itself, in the shape the UI labels a Service.</summary>
    public string DeclaredIdentity => $"{Namespace}/{EnvironmentName}/{ServiceName}";

    public static int PrintError(OptionsError error)
    {
        Console.Error.WriteLine($"error: {error.Message}");
        Console.Error.WriteLine();
        Console.Error.WriteLine(Usage);
        return 2;
    }

    private static string Next(string[] args, ref int i)
        => ++i < args.Length ? args[i] : throw new OptionsError($"Option '{args[i - 1]}' expects a value.");
}

internal sealed class OptionsError(string message) : Exception(message);
