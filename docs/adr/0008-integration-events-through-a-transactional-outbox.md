---
status: accepted
---

# Contexts learn each other's facts through a transactional outbox

When one context commits a fact the others must act on — today only the Deletion of a Service or an Application — it records an integration event in an outbox table inside the very transaction that commits the fact. A dispatcher owned by the composition root drains the outbox and hands each event to whichever contexts registered a handler for it. The publishing context never learns who listens; the consuming contexts never learn who published.

## Considered Options

- **Direct calls between contexts** — impossible in the direction we need: `Bugler.Ingestion` already references `Bugler.Registry` to validate API keys, so Registry cannot reference Ingestion back, and the architecture tests forbid Registry and Access from depending on any context at all.
- **Publishing in-process, after the commit** — a handful of lines and no new table, but a handler that fails leaves the fact committed and its consequences undone, with no record that anything is owed. The admin is told the telemetry is gone while it silently is not.
- **Reconciliation only, through the existing purge job** — Registry deletes its rows and Ingestion notices, on its next sweep, that telemetry belongs to a Service no longer in the Catalog. Genuinely zero new machinery, but the data outlives the click by up to the purge interval (an hour by default).
- **PostgreSQL `LISTEN`/`NOTIFY` to wake the dispatcher** — the right answer once publisher and dispatcher are separate processes; today it buys the same latency as an in-memory signal in exchange for a connection held open and re-established.

## Consequences

- Delivery is at-least-once, so every handler must be idempotent. Both current handlers are, being deletes by identifier.
- Each publishing context owns its own outbox table in its own schema, because the point of the pattern is that the insert shares the publisher's transaction. A single shared table would have to be written outside it.
- The dispatcher wakes on a signal from the publisher and on a timer, so the erasure normally begins milliseconds after the request and still happens if the signal is lost to a crash.
- A message that keeps failing backs off exponentially, and is parked as `failed` after ten attempts rather than retried forever. Messages are handled independently, so a parked one blocks nothing. Successful messages are deleted: the steady state of the table is empty, and anything in it means something needs attention.
- The outbox is delivery infrastructure and not an audit log — nothing in it records who acted (see ADR 0007).
- Telemetry can still be orphaned in ways the outbox cannot see: Signals buffered in memory before the Deletion are written after the erasure ran. The retention purge therefore also reclaims telemetry whose Service is no longer in the Catalog, bounded by the moment it read the Catalog so a newly registered Service is never caught.
- `Bugler.SharedKernel` now holds the integration event contracts alongside the shared identifiers. It still depends on nothing.
