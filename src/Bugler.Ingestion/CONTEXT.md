# Ingestion

The write path of Bugler: the only place telemetry enters the system. Receives OTLP export requests, establishes which instance sent them, stores the telemetry, and removes it when its retention expires.

## Language

**Export Request**:
A single OTLP submission (logs or traces) received from a Source.
_Avoid_: message, payload, upload

**Signal**:
One unit of telemetry — a log record or a span; metrics are a future signal kind.
_Avoid_: event, entry, data point

**Source**:
The Instance an Export Request provably came from, established by its API key — never by what the payload claims about itself.
_Avoid_: sender, client, origin

**Batch**:
A group of Signals persisted together as one write.
_Avoid_: chunk, buffer

**Rejection**:
The refusal of an Export Request — because its API key is invalid, its payload is malformed, or the system is overloaded.
_Avoid_: error, drop

**Purge**:
The permanent removal of Signals that have outlived their Instance's retention.
_Avoid_: cleanup, deletion job, GC
