# Bugler

Self-hosted telemetry server: collects **logs and traces** via the OpenTelemetry protocol (OTLP) and lets a team explore and correlate them in a web UI. Built for the situation where a company runs **multiple applications, each deployed separately for multiple clients**, and team members may only see the telemetry of the applications they are granted.

Metrics support is planned for a later phase.

## Shape

- Single-process **modular monolith** (.NET 10, ASP.NET Core): OTLP ingest (gRPC :4317, HTTP :4318), REST API, and the frontend served from one container.
- **PostgreSQL** as the only backing store (telemetry + catalog + users).
- **React + TypeScript** frontend (Bun toolchain, shadcn/ui, TanStack Router/Query).
- Distributed via **Docker + docker-compose**.

## Domain

The domain hierarchy is **Application → Service → Tenant**:

- An *Application* is a product; user access is granted per application.
- A *Service* is one registered sender — one role of one deployment, identified by its namespace, environment and name (`demo/prod · backend`). It owns its API keys and retention, and its identity is what the key proves, never what the telemetry claims about itself ([ADR 0006](docs/adr/0006-service-is-the-sender-identity.md)).
- A *Tenant* is a customer inside a multi-tenant Service, visible only as a filter attribute in telemetry.

The codebase is split into four bounded contexts — **Ingestion**, **Exploration**, **Registry**, **Access** — described in [CONTEXT-MAP.md](CONTEXT-MAP.md), each with its own glossary (`src/Bugler.<Context>/CONTEXT.md`). Architectural decisions are recorded as ADRs in [docs/adr](docs/adr).

## Running it

```bash
docker compose up --build -d
```

Open http://localhost:8080 — the first account created becomes the server administrator.
Register an application and a service in **Admin**, issue an API key, and point your
services at the server:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-server:4317
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_..."
```

See [Sending telemetry](#sending-telemetry) for the details, and for the two mistakes that
make an exporter drop everything without saying a word.

## Sending telemetry

Bugler is a plain OTLP endpoint. Anything that speaks the protocol can export to it — an
OpenTelemetry SDK in any language, a logging-library sink such as `Serilog.Sinks.OpenTelemetry`
or the OpenTelemetry Logback appender, or a Collector forwarding on your behalf. Bugler is not
involved in the choice.

| | |
| --- | --- |
| OTLP/gRPC | `:4317` — one address for every signal |
| OTLP/HTTP | `:4318` — `POST /v1/logs`, `POST /v1/traces` |
| Signals | **logs and traces**; metrics are not implemented yet |
| Authentication | `Authorization: Bearer blgr_…` on every export |
| Body | protobuf only — JSON-encoded OTLP is refused with `415` |

Register an Application and a Service in **Admin** and issue that Service an API key. The key *is*
the sender's identity: `service.name` and the rest of the resource attributes are stored and shown,
but never decide which Service the data belongs to ([ADR 0006](docs/adr/0006-service-is-the-sender-identity.md)).
One key per deployed role — reusing a key across two deployments merges their telemetry.

### The portable way

Every OpenTelemetry SDK reads these variables, so they work regardless of language or framework:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-server:4317
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_..."
# for OTLP/HTTP instead of gRPC:
#   OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
#   OTEL_EXPORTER_OTLP_ENDPOINT=http://your-server:4318
```

Configuring an exporter in code works equally well — but read the next two sections first, because
both failure modes below are silent. An exporter that cannot deliver drops the batch and lets the
application run on; nothing appears in Bugler and nothing appears in your logs.

### Base address, or full signal path?

Exporters disagree about what an endpoint setting means, and the disagreement is invisible until
telemetry goes missing.

| Exporter | Give it |
| --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, any SDK | **base** — the SDK appends `/v1/logs`, `/v1/traces` itself |
| OpenTelemetry .NET, `OtlpExporterOptions.Endpoint` assigned in code | **full per-signal URL** — the SDK appends nothing |
| `Serilog.Sinks.OpenTelemetry` 4.x, `options.Endpoint` | **base** — the sink derives `LogsEndpoint`/`TracesEndpoint` |

Getting it wrong lands you on `/` or on `/v1/logs/v1/logs`; both answer `404`/`405` and the batch
is gone.

**With gRPC the question does not arise** — signals are routed by service name, so
`http://your-server:4317` is correct for every exporter. Prefer gRPC unless something forces HTTP.

### One setting shared by two exporters

Applications often export logs through one library and traces through another — a Serilog sink
beside an OTel SDK, for instance. If both read the *same* endpoint setting, a full HTTP path cannot
satisfy them: the exporter that appends nothing will send **traces and metrics to `/v1/logs`**.
Bugler decodes those as a malformed log batch and rejects the whole request with `400`, discarding
the genuine log records travelling in it. Give each exporter its own setting, or use gRPC.

### When nothing arrives

Check the status Bugler returned before suspecting Bugler:

| Status | Meaning |
| --- | --- |
| `401` / gRPC `UNAUTHENTICATED` | missing key, or no Service matches it |
| `404` / `405` | wrong path — only `POST /v1/logs` and `POST /v1/traces` exist |
| `415` | `Content-Type` is not `application/x-protobuf` |
| `400` | body is not a decodable OTLP payload *for that signal* |
| `503` / gRPC `UNAVAILABLE` | ingest buffer full; the exporter should retry |
| gRPC `UNIMPLEMENTED` | metrics — Bugler has no metrics receiver yet |

Exporters hide these by design, so turn their own diagnostics on: `Serilog.Debugging.SelfLog.Enable(…)`
for a Serilog sink, the equivalent self-diagnostics channel for your SDK. From Bugler's side, running
it with `Logging__LogLevel__Microsoft.AspNetCore=Information` logs every request with its status code,
which is usually the fastest way to see what a silent exporter is really sending.

A quick way to prove the server, the key and the network before blaming your application:

```bash
curl -i -X POST http://your-server:4318/v1/logs \
  -H "Content-Type: application/x-protobuf" \
  -H "Authorization: Bearer blgr_..." --data-binary ''
```

An empty body is a valid, empty OTLP request: `200` means the path and the key are good.

## Development

Prerequisites: .NET 10 SDK, Bun, Docker.

```bash
docker compose up -d          # local PostgreSQL
dotnet run --project src/Bugler.Host
cd frontend && bun dev        # frontend dev server with HMR
```

The Host listens on `:8080` (API/UI), `:4317` (OTLP/gRPC), and `:4318` (OTLP/HTTP). Database schema migrates automatically at startup. Each process authenticates its exports with its Service API key as a bearer token, e.g. `OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_..."`.

### Tests

| Layer | Where | Run |
| --- | --- | --- |
| Unit (backend) | `tests/Bugler.<Context>.Tests` | `dotnet test` |
| Architecture | `tests/Bugler.ArchitectureTests` (ArchUnitNET) + `frontend/.dependency-cruiser.cjs` | `dotnet test` / `bun run arch` |
| Integration (real PostgreSQL via Testcontainers) | `tests/Bugler.IntegrationTests` | `dotnet test` (needs Docker) |
| Unit (frontend) | `frontend/**/*.test.tsx` | `bun test` |
| E2E (Playwright) | `e2e/tests` | `cd e2e && bun run test` (needs `docker compose up -d postgres`) |

Architecture tests enforce the context boundaries described in [CONTEXT-MAP.md](CONTEXT-MAP.md); a dependency that crosses a context boundary outside its `Contracts` namespace fails the build.

## Status

Early development — scaffolded solution; domain implementation in progress.
