---
status: accepted
---

# Alerting polls the telemetry schema

`Bugler.Alerting` discovers trouble by periodically querying `telemetry.log_records` — every 15–30 seconds, from a persisted cursor forward, grouped per Service against each Service's current effective Sensitivity. It is the second reader of the tables Ingestion owns, on the terms ADR 0009 established for Exploration: Ingestion alone writes and migrates `telemetry.*`, Alerting only ever reads, and neither references the other's assembly.

The rejected alternative is telling Alerting about logs as they arrive. An in-process notification from the ingest write path would alert a second sooner, but it lives in memory: a crash between flush and evaluation loses the nudge, and a silently missed Episode is the worst failure an alerting feature can have. Making the nudge durable through the outbox (ADR 0008) is out of the question at telemetry volume. And the write path — the part of Bugler that must never slow down — would come to know that alerting exists. Polling inverts every one of those properties: a crash means a late catch-up read instead of a lost Episode, and ingest cannot tell whether Alerting is running at all. The Quiet Window needs a periodic evaluation anyway — no event marks "nothing happened for fifteen minutes" — so one loop carries detection, Episode closing, and Delivery, and the push design would have needed the same loop besides its events.

## Consequences

- Alert latency is bounded below by the poll interval. Deliberately acceptable: mail and chat delivery already cost seconds, and human reaction costs minutes.
- The cursor rides the `bigint` identity of `log_records`, and identity values become visible out of commit order under concurrency — so the poll must re-read a small overlap behind the cursor (or Ingestion grows a server-side arrival timestamp). Implementation detail, but a correctness-critical one; the integration tests must cover it.
- A schema change to `telemetry.log_records` now breaks two readers, and nothing automated catches either (ADR 0009's caveat, doubled). Indexes that only the alerting poll wants live in Ingestion's migrations, named for the query that wants them.
- Alerting persists its own state — cursor, Episodes, settings, Subscriptions, Deliveries — in its own `alerting` schema through its own `DbContext`, like every other context.
