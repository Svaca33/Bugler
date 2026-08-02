---
status: accepted
---

# Storage is reported by the write path

The admin's Storage view answers two questions per Service — how much disk its stored Signals
hold (Footprint) and how fast they are adding to it (Ingest Rate) — each beside the Effective
Retention that bounds it, so the setting and its cost sit in one table. Three decisions shape
where and how that answer is produced.

**Ingestion owns the report.** `GET /api/admin/storage` is served by the write path, not by
Exploration. Storage cost is Ingestion's own operational truth: its tables, its purge, its
schema to approximate over. Exploration's language is about the *content* of telemetry within a
user's Visibility Scope; this view ignores grants and speaks of disk, which that language has
no words for.

**The report includes Effective Retention itself, rather than leaving it to a browser join.**
ADR 0013 keeps joined reads out of the server where contexts share no relationship — Exploration
and Alerting have none, so the Dashboard joins in the client. Here the relationship already
exists and is precisely about this number: Ingestion reads `IRetentionReader` when purging, and
the days shown beside each Footprint are exactly what the purge works from, reported by the one
that works from it. The Host mounts the endpoint group and names its capability
(`InspectStorage`), because authorization vocabulary is Access's and the context map gives
Ingestion no path to it.

**Bytes are estimates, and labelled.** Every Service's Signals share two heaps, so exact
per-Service bytes do not exist cheaply. The report takes exact row counts per Service (an
index-only scan per table), measures the last day's rows honestly (`pg_column_size` over the
window, a bounded heap read), prices each Service's history at the average row width it showed
in that window, and scales the shares so they sum to `pg_total_relation_size` — data, indexes
and TOAST together. Rejected: an exact whole-table scan (reads everything retention exists to
bound), maintained counters (drift, and the batched purge would have to account for every batch
it takes), and planner statistics alone (rows would be approximate too, and the MCV list caps
how many Services it can see).

## Consequences

- One request walks each telemetry index end to end and the heap behind one day of Signals,
  under a 30 s ceiling. That is accepted because only an admin asks, and rarely; if it ever
  hurts, pre-aggregation is the revisit point — the same one ADR 0009 already names.
- History is priced at current behaviour: a Service whose rows were much wider last month than
  today is misjudged by exactly that difference. Deliberate — "an estimate of current
  behaviour" is the promise the view makes.
- Week and month figures are client-side extrapolations of the measured day, labelled as
  projections; nothing is measured beyond the day, because retention has usually already eaten
  it.
- Signals orphaned by a Deletion keep their share of the table until the purge reclaims them;
  the report does not name them, but it does not smear their bytes over the registered either.
- The seams stay honest: Registry sets retention, Ingestion prices it, and the browser joins
  Service names from the catalog — ADR 0013's spine, unchanged.
