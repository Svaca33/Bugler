---
status: accepted
---

# The log list is served by a timestamp-leading index

`telemetry.log_records` carries `(timestamp DESC, service_id, severity_number)`, and the GIN indexes on the `attributes` columns of both telemetry tables are dropped.

Measured against 20 million rows at the density the first production deployment expects — roughly 11 million Log Records a day, so 11.3 million inside a 24-hour window — the log list took **26.5 seconds** and one screen of the Logs page, which issues the list, the total and the Volume together, took about **42 seconds**. The plan was a `Parallel Seq Scan` over 1.8 million buffers followed by a top-N sort, to return a hundred rows.

The existing `(service_id, timestamp DESC)` index cannot serve that query. Exploration filters with `service_id = ANY(@services)` — a Visibility Scope is a set, never one Service — and orders globally by `timestamp DESC`. Walking that index in the ordering the query asks for would mean merging one ordered scan per Service, which PostgreSQL will not do here; it reads everything and sorts instead. Putting `timestamp` first inverts that: the index already stands in the order the query wants, so `LIMIT` stops the scan as soon as it has its rows. The same query then plans as an `Index Scan` reading **101 rows in 10 milliseconds**. `service_id` and `severity_number` ride along so the Source Filter and the Volume's severity bands are answered from the index rather than from the heap, which at ~750 bytes a row is where the cost lives: the Volume fell from 7.3 seconds to 2.0, the total from 8.7 to 0.7.

The GIN indexes go because nothing reads them. [ADR 0001 of Exploration](../../src/Bugler.Exploration/docs/adr/0001-attribute-filter-semantics.md) chose text equality over typed containment deliberately, and text equality cannot use GIN; no `@>` exists anywhere in `src/`, and the measurement confirmed zero scans. They were 397 MB at 20 million rows and were paid for on every insert.

## Consequences

- The new index costs about 945 MB per 20 million Log Records — roughly 3.5 GB at the retention and volume above. Against a table of some 73 GB that is the cheapest item in the budget, and it replaces two indexes that bought nothing.
- The index pays off only while the **visibility map** is current: an index-only scan is "only" over pages `VACUUM` has marked all-visible, and telemetry is written and never updated, so autovacuum's proportional defaults would leave more than a day of the newest rows unmarked — precisely the range every query asks about. Left alone, the planner reads the whole table instead, which is the 26.5 seconds this decision exists to remove. Both telemetry tables therefore carry a one percent autovacuum threshold, set by the migration that follows this one; the index and that setting are one decision in two statements.
- A Filter that matches **nothing** still walks the whole window: `LIMIT` can only stop early once it has found rows, so a search for a string that is absent stays a full scan (17 seconds in the measurement). A trigram index on `body` is the answer if that ever hurts enough to pay for it.
- `ix_log_records_service_id_timestamp` took zero scans once this index existed, but is kept. The measurement only ever filtered on several Services at once, which is what a Visibility Scope produces; a Filter narrowed to a single Service may still prefer it. Dropping it is its own decision and wants its own measurement.
- **Spans are not covered by this.** The traces list groups by `trace_id` and orders by `min(start_time)`, an aggregate that `LIMIT` cannot short-circuit — it has to read the whole window before it knows which traces are the newest. A timestamp-leading index therefore does not transfer, and mirroring one onto `spans` on the strength of the log result would repeat exactly the mistake this decision removes. The traces list is left unmeasured, deliberately.
- The Volume stays at about two seconds because it genuinely counts eleven million rows; no index makes that free. Pre-aggregation is the next lever, and [ADR 0009](0009-exploration-reads-the-telemetry-schema-directly.md) already names it as the case that would reopen Exploration's read model.
- Following ADR 0009, the index is added by an Ingestion migration and carries a comment naming the Exploration query that wants it, so it does not read as unexplained write-path overhead.
