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

**Declared Version**:
What a Service claims to be running, in `service.version` — a claim like the rest of its Declared Identity, never a registered fact. Compared for equality alone and never ordered: OTel leaves the format open, so `2.0.0` and `a01dbef8a` are both valid and neither is "newer" than the other. A Service that declares none has none, and nothing is inferred in its place.
_Avoid_: version number, build, tag, revision

**Release**:
The instant a Service began reporting a Declared Version it was not already running. Observed from arriving telemetry rather than announced, and equally from logs and from traces. The first Declared Version ever seen of a Service is not a Release — there is nothing it followed. A return to an earlier version is an ordinary Release, because versions are not ordered. Outlives retention, unlike the Signals it was read from, and dies only with the Service's Deletion.
_Avoid_: deployment, rollout, version bump, ship

**Batch**:
A group of Signals persisted together as one write.
_Avoid_: chunk, buffer

**Rejection**:
The refusal of an Export Request — because its API key is invalid, its payload is malformed, or the system is overloaded.
_Avoid_: error, drop

**Purge**:
The permanent removal of Signals that have outlived their Service's retention.
_Avoid_: cleanup, deletion job, GC

**Erasure**:
The permanent removal of a Service's Signals because the Service itself is gone, as opposed to a Purge, which is driven by retention alone.
_Avoid_: purge, cleanup, wipe

**Footprint**:
How much storage one Service's stored Signals occupy, in bytes — split into logs and traces, because each half answers to its own retention clock. An estimate by design: every Service's Signals share the same tables, so the report divides what the tables really occupy instead of measuring each Service exactly, and it is labelled as such wherever it is shown.
_Avoid_: usage, size, disk space

**Ingest Rate**:
How fast a Service is adding bytes, measured from the stored Signals of the last day — never from a counter at the door. Figures quoted beyond the measured day (a week, a month) are extrapolations of current behaviour and say so.
_Avoid_: throughput, traffic, volume

**Settled Footprint**:
Where a Service's Footprint comes to rest if its Ingest Rate holds: one day's bytes times the Effective Retention, the log and trace clocks each on their own. What a retention change is best judged against — the knob and its price in one number.
_Avoid_: steady state, projected size
