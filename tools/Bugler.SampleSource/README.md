# Bugler.SampleSource

A sample telemetry Source for manual testing: simulates a small e-shop and streams
its traces + correlated logs into a running Bugler over OTLP (HTTP or gRPC).

Each operation is a realistic little trace — an HTTP server span with nested
db/cache/payment child spans — and the logs are emitted inside the active span, so
they arrive trace-correlated. Roughly 15 % of checkouts fail (error span with an
exception event + error log) and a few operations run slow (warning logs), which
gives the Exploration UI something interesting to show.

## Usage

Create an application + service in the Bugler UI (or via the REST API), issue an
API key, then:

```bash
dotnet run --project tools/Bugler.SampleSource -- --api-key blgr_...
```

| Option | Default | Meaning |
| --- | --- | --- |
| `--api-key <key>` | `BUGLER_API_KEY` env var | Service API key |
| `--protocol <p>` | `http` | `http` (OTLP/HTTP, port 4318) or `grpc` (OTLP/gRPC, port 4317) |
| `--endpoint <url>` | `http://localhost:4318` / `:4317` | OTLP endpoint |
| `--rate <ops/s>` | `2` | Target operations per second |
| `--count <n>` | run until Ctrl+C | Stop after N operations |
| `--service <name>` | `sample-eshop` | `service.name` resource attribute |
| `--quiet` | off | Suppress per-operation output |

Run several copies with different `--service` names and API keys to fill more than
one Service at once. Note that Bugler files the telemetry under the Service the key
belongs to — `--service` only sets the `service.name` resource attribute, which is
stored but never used for identity (ADR 0006).

Export failures (wrong endpoint, revoked key) are printed to stderr as
`[otel Error] …` lines — the OpenTelemetry SDK would otherwise swallow them.

For a single fixed batch sent as raw OTLP protobuf without the OpenTelemetry SDK,
see [send-sample-telemetry.cs](../send-sample-telemetry.cs).
