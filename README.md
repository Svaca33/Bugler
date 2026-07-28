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
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-server:4318
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_..."
```

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
