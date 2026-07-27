---
status: accepted
---

# PostgreSQL as the single store for telemetry and metadata

Bugler stores logs, traces, and all metadata (catalog, users, grants) in one PostgreSQL database. Expected volume is small — units of GB per day from a handful of services — so a columnar OLAP store would add a second system to operate without a performance need; JSONB attributes, GIN indexes, and daily time partitioning cover search and retention.

## Considered Options

- **ClickHouse** — better analytical performance at scale, but a second system to run (metadata would still need a relational DB) and heavier local development. Revisit if ingest approaches ~100 GB/day or query latency degrades.
- **SQLite** — zero ops, but a team server with concurrent ingest and reads outgrows a single-writer embedded DB.

## Consequences

Retention is implemented by dropping daily partitions, with per-instance overrides deleting rows inside the window. All modules share one database but each context owns its tables exclusively.
