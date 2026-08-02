---
status: accepted
---

# A refused Batch is salvaged, not lost

A Batch is one binary COPY, and a COPY is all or nothing: PostgreSQL refusing one value in one row reverts every row that travelled with it. Until now the writers took that at face value and logged `batch lost`, up to `Ingestion:MaxBatchSize` — 5 000 — Signals at a time. Now a refused Batch is halved and each half re-attempted, recursively, until the Signals PostgreSQL will not hold stand alone. Those are dropped and logged with the Service that sent them; everything else is stored.

**Why this is not the loss ADR 0003 accepts.** That loss is bounded and accidental: a database that went away for a moment, a process that crashed, a few seconds of telemetry. This one was neither. `TelemetryBuffer` is one channel per signal kind shared by every Service, so a Batch is drawn from whoever happened to be sending — and most of what died with a refused one belonged to Services that had sent nothing wrong. It repeated for as long as the sender kept sending, and nobody was told: the Export Request is answered with success once it is buffered, long before the Batch is written, so there is no Rejection to see and the only trace was a line in the container log. One application logging a byte array could silence the ingest of the whole server.

**Why halving rather than row by row.** Both salvage everything salvageable; they differ in what they cost when it happens, and it happens on every Batch for as long as the sender misbehaves. One bad Signal in a Batch of 5 000 costs about 26 COPY attempts halved against 5 000 retried singly. The saving is the whole point: a deterministic sender turns the failure path into the normal path.

**Why only a refusal, and never a silence.** Halving is only sensible when PostgreSQL answered — an answer means the Batch arrived and something inside it was unacceptable, so some of it is probably fine. When PostgreSQL cannot be reached at all, every Signal in the Batch is equally doomed and halving would spend its budget opening connections that cannot be opened, at exactly the moment the database is least able to afford it. So a `PostgresException` is salvaged and anything else is logged and dropped, which is ADR 0003's behaviour, unchanged.

**Why there is a ceiling.** Halving is cheap because failures are rare within a Batch; it degenerates when they are not. A Batch in which every row fails — a full disk, not bad data — would cost about 10 000 attempts. A Salvage may therefore spend 128 COPY attempts, and gives the rest of the Batch up when they run out. That is enough to isolate a handful of bad Signals in a full Batch with room to spare, and past it the case is not one sender logging a stray byte but something systemic, which belongs under bounded loss rather than under a dissection.

**This is a seatbelt, not a cure.** The refusal that prompted it — a NUL, which no PostgreSQL text or jsonb column will hold — is cured where it belongs, in the mappers, by replacing the character with U+FFFD before the row is built. The Salvage is what stands behind that for the refusal we have not met yet, including the one somebody introduces by adding a field to a row and forgetting to tame it.

## Consequences

- A Signal can now be dropped alone, and the only account of it is an error in the container log naming its Service, its SQLSTATE, and nothing else. The Signal itself is not logged: it is the sender's, and unbounded in size.
- The sender is still told nothing, because it was told `success` at the buffer. Nothing here changes that, and any future answer would have to come from somewhere else entirely.
- `log_records.id` gains gaps. A refused COPY still consumes sequence values, and a salvaged Batch is written after it. Nothing reads those ids as contiguous — Alerting's poll is a high-water mark, and salvaged rows land above it, never behind it.
- Order within a Batch survives: halves are attempted left to right, so a Batch's Signals still reach the table in the order they were buffered.
- The failure path now costs round trips where it used to cost one. It is only reached after a refusal, and the ceiling bounds it.
