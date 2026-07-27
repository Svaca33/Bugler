---
status: accepted
---

# Single-process modular monolith with direct OTLP ingest

Bugler ships as one ASP.NET Core process that terminates OTLP itself (gRPC :4317 and HTTP :4318), serves the REST API, and hosts the frontend — no OpenTelemetry Collector required in front, no message queue, no separate ingest service. At the target volume, splitting the write and read paths into separate deployables would double the operational surface for no benefit.

## Considered Options

- **Separate ingest and query services** — independent scaling, rejected as premature.
- **Collector-fronted ingest** (Bugler exposes only an internal API) — less protocol work, but makes the Collector a mandatory component of every deployment.
- **Queue-based pipeline (Kafka/RabbitMQ)** — spike resilience an in-process channel already provides at this volume.

## Consequences

Internal structure must keep extraction possible: four bounded contexts (Ingestion, Exploration, Registry, Access) live in separate assemblies, communicate only through public interfaces and shared ids, and architecture tests enforce these boundaries.
