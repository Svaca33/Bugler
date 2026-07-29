---
status: accepted
---

# Exploration reads the telemetry schema directly

`Bugler.Exploration` issues SQL straight against `telemetry.log_records` and `telemetry.spans`, the tables `Bugler.Ingestion` owns and migrates. The boundary between the two contexts is therefore not the database but **write ownership**: Ingestion alone writes and migrates `telemetry.*`, Exploration only ever reads it, and neither references the other's assembly (the architecture tests enforce that part). This was decided implicitly when the read path was first written; it is recorded now because the Volume feature makes it consequential — an index that only an Exploration query wants has to be added by an Ingestion migration.

The alternative is a read model of Exploration's own, projected from the telemetry as it is ingested. It was rejected because there is nothing to project: Exploration answers questions about exactly the rows Ingestion stores, in the shape it stores them. A projection would duplicate the two largest tables in the system — the ones retention exists to keep bounded — to answer no question the originals cannot, and would put the read path's availability behind a projector that can fall behind or fail. For a self-hosted single-process deployment that trade is plainly bad.

## Consequences

- `CONTEXT-MAP.md` used to claim no context reads another's data store. It was never true of Exploration and has been corrected rather than the code bent to fit it.
- A schema change to `telemetry.*` is a breaking change for Exploration, and **nothing automated catches it**: the architecture tests inspect assembly references, not SQL strings. The integration tests are the only net.
- Indexes serving Exploration queries live in Ingestion's migrations, and carry a comment naming the query that wants them — otherwise they read as unexplained write-path overhead.
- Exploration holds no `DbContext` over telemetry and takes an `NpgsqlDataSource` instead, which keeps "read-only" a property of the code rather than a promise in a document.
- If Exploration ever needs telemetry shaped differently from how it is stored — pre-aggregated Volume for very wide windows is the plausible first case — that is the point to revisit this, and it would be a new decision rather than a violation of this one.
