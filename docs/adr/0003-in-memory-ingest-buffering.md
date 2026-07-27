---
status: accepted
---

# In-memory batched ingest buffer with a bounded loss window

Export requests are acknowledged once enqueued into an in-process bounded buffer; a background writer flushes batches to PostgreSQL roughly every second. A process crash therefore loses at most a few seconds of telemetry — acceptable for logs and traces, and OTel SDKs retry failed exports. When the buffer is full, ingest responds 503 with Retry-After instead of dropping silently.

## Considered Options

- **Synchronous per-request writes** — zero loss, but an order of magnitude less throughput and every database hiccup propagates to every sending application.
- **Local write-ahead log on disk** — survives crashes, but rotation/replay/corruption handling is real complexity for a guarantee telemetry rarely needs.
