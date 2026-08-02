# Bugler.SampleSource

A sample telemetry Source for manual testing: simulates a small e-shop and streams
its traces + correlated logs into a running Bugler over OTLP (HTTP or gRPC).

Each operation is a realistic little trace — an HTTP server span with nested
db/cache/payment child spans — and the logs are emitted inside the active span, so
they arrive trace-correlated. Roughly 15 % of checkouts fail (error span with an
exception event + error log) and a few operations run slow (warning logs), which
gives the Exploration UI something interesting to show. On top of that steady
trickle, an inventory sync fails with a different error every five-plus minutes —
rare enough for Alerting's quiet window to close the Episode in between.

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
| `--decline-rate <p>` | `15` | Percent of checkouts that fail with a payment decline; `0` silences the steady error trickle |
| `--rare-error-minutes <m>` | `5` | At least this many minutes between inventory-sync failures; `0` disables them |
| `--quiet` | off | Suppress per-operation output |

An Episode is per Service, so while the payment declines keep one Service's Episode
open, a rarer error to the same Service only raises its counts. To watch quiet
windows open and close Episodes, point a second copy at another Service with the
trickle off:

```bash
dotnet run --project tools/Bugler.SampleSource -- \
  --api-key blgr_other… --decline-rate 0 --rare-error-minutes 5
```

## Declared identity

Bugler files every signal under the Service its API key belongs to; what the payload
says about itself is kept as ordinary resource attributes and never establishes
identity ([ADR 0006](../../docs/adr/0006-service-is-the-sender-identity.md)). These
options only set those attributes, so point them at the facets of the Service the key
was issued for and the sample data will not contradict its own registration:

| Option | Default | Resource attribute |
| --- | --- | --- |
| `--namespace <ns>` | `demo` | `service.namespace` — Service Namespace |
| `--environment <e>` | `sample` | `deployment.environment.name` — Environment |
| `--service <name>` | `sample-eshop` | `service.name` — Service Name |
| `--replica <id>` | machine name | `service.instance.id` — Replica |
| `--version <v>` | `1.0.0` | `service.version` — Declared Version |

```bash
dotnet run --project tools/Bugler.SampleSource -- \
  --api-key blgr_… --namespace acme --environment prod --service backend
```

Run several copies with different keys to fill more than one Service at once, or
several copies with one key and different `--replica` values to simulate the replicas
of a single Service.

`--version` is the one of these Bugler does read: a change of it is observed as a
Release and drawn as a marker on the Volume and beside the Episodes that opened after
it ([ADR 0016](../../docs/adr/0016-releases-are-observed-at-ingest.md)).

By default it does not stay put — `--version-every 1` raises its trailing number every
minute, so a run left going produces a Release a minute and the markers keep arriving
without anything being restarted. `--version-every 0` holds one version for the whole
run instead, and `--version ""` declares none at all, which is what a Service that never
sets the attribute looks like: no markers, and nothing else about it changes.

A raise rebuilds the OTLP providers, because a Resource is fixed when its provider is
built. The old one is flushed before the new one starts, so the handover is clean and
each version's telemetry lands whole — unlike a real rolling deploy, where the two
overlap for minutes.

Export failures (wrong endpoint, revoked key) are printed to stderr as
`[otel Error] …` lines — the OpenTelemetry SDK would otherwise swallow them.

For a single fixed batch sent as raw OTLP protobuf without the OpenTelemetry SDK,
see [send-sample-telemetry.cs](../send-sample-telemetry.cs).
