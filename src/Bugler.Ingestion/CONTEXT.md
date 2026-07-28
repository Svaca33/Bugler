# Ingestion

The write path of Bugler: the only place telemetry enters the system. Receives OTLP export requests, establishes which Service sent them, stores the telemetry, and removes it when its retention expires.

## Language

**Export Request**:
A single OTLP submission (logs or traces) received from a Source.
_Avoid_: message, payload, upload

**Signal**:
One unit of telemetry — a log record or a span; metrics are a future signal kind.
_Avoid_: event, entry, data point

**Source**:
The Service an Export Request provably came from, established by its API key — never by what the payload claims about itself.
_Avoid_: sender, client, origin

**Declared Identity**:
What an Export Request says about its own origin (`service.name`, `service.namespace`, `deployment.environment.name`). Kept as ordinary attributes and never used to establish the Source, so a sender misnaming itself stays visible instead of being believed.
_Avoid_: reported service, claimed name

**Batch**:
A group of Signals persisted together as one write.
_Avoid_: chunk, buffer

**Rejection**:
The refusal of an Export Request — because its API key is invalid, its payload is malformed, or the system is overloaded.
_Avoid_: error, drop

**Purge**:
The permanent removal of Signals that have outlived their Service's retention.
_Avoid_: cleanup, deletion job, GC
