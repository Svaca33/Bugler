# Bugler

Self-hosted telemetry server: collects **logs and traces** via the OpenTelemetry protocol (OTLP) and lets a team explore and correlate them in a web UI. Built for the situation where a company runs **multiple applications, each deployed separately for multiple clients**, and team members may only see the telemetry of the applications they are granted.

Metrics support is planned for a later phase.

## Shape

- Single-process **modular monolith** (.NET 10, ASP.NET Core): OTLP ingest (gRPC :4317, HTTP :4318), REST API, and the frontend served from one container.
- **PostgreSQL** as the only backing store (telemetry + catalog + users).
- **React + TypeScript** frontend (Bun toolchain, shadcn/ui, TanStack Router/Query).
- Distributed via **Docker + docker-compose**.

## Domain

The domain hierarchy is **Application → Instance → Tenant**:

- An *Application* is a product; user access is granted per application.
- An *Instance* is one client deployment; it owns its API key and retention.
- A *Tenant* is a customer inside a multi-tenant instance, visible only as a filter attribute in telemetry.

The codebase is split into four bounded contexts — **Ingestion**, **Exploration**, **Registry**, **Access** — described in [CONTEXT-MAP.md](CONTEXT-MAP.md), each with its own glossary (`src/Bugler.<Context>/CONTEXT.md`). Architectural decisions are recorded as ADRs in [docs/adr](docs/adr).

## Status

Early development — documentation-first bootstrap; code scaffolding follows.
