# Exploration

The read path of Bugler: lets an authorized person search, view, and correlate stored telemetry. Strictly read-only over telemetry data.

## Language

**Log Record**:
A stored log signal with its timestamp, severity, body, and attributes.
_Avoid_: log entry, log line, message

**Trace**:
The set of Spans sharing one trace id, describing a single request's journey through one or more services.
_Avoid_: request flow, transaction

**Span**:
One operation within a Trace, with its own timing and attributes.
_Avoid_: segment, step

**Waterfall**:
The hierarchical timing view of a Trace's Spans.
_Avoid_: flame graph, Gantt, timeline view

**Filter**:
The criteria narrowing displayed telemetry: Source Filters, the Time Filter, severity, full-text, and Attribute Filters. Tenant is not a first-class criterion — it is filtered via an Attribute Filter on `tenant.id`.
_Avoid_: query, search criteria

**Source Filter**:
A Filter criterion narrowing which Services a query may touch — Application, Service Namespace, Environment or Service Name — each one left open meaning "all". Unlike an Attribute Filter it matches registered facts, so it cannot be fooled by what telemetry claims about itself.
_Avoid_: scope filter, instance filter, source picker

**Time Filter**:
A Filter criterion bounding when the telemetry happened, by the timestamp its sender stamped on it. Either a Relative Range or an Absolute Range, never both; absent, it constrains nothing.
_Avoid_: time window, period, interval, lookback

**Relative Range**:
The Time Filter expressed as a fixed-length duration back from "now", resolved against the server's clock rather than the viewer's. A week is 7 days and a month 30 — no calendar arithmetic, so no time zone. It only sets the lower bound: telemetry stamped in the future stays visible instead of vanishing.
_Avoid_: last N, since, relative time

**Absolute Range**:
The Time Filter expressed as instants. Either end may be left open, and each end carries its own UTC offset so it cannot be read in the wrong zone.
_Avoid_: custom range, fixed range, from/to

**Resolved Window**:
The two instants a Time Filter actually came out as, decided by the server's clock and reported back with the answer. An open end resolves to what the matching telemetry reaches, so a window is always nameable even when the Filter left it unbounded. Viewers never compute it themselves — their clocks disagree.
_Avoid_: effective range, computed range, actual window

**Attribute Filter**:
A Filter criterion matching one attribute — identified by its scope (signal attribute vs Resource Attribute) and its path — against an exact value. Combined with AND across keys; one value per key at a time.
_Avoid_: property filter, tag filter, key-value filter

**Resource Attribute**:
An attribute describing the entity that emitted the telemetry (service, host, deployment) rather than the individual Log Record or Span.
_Avoid_: tags, metadata, resource field

**Observed Keys**:
The attribute keys present in a recent sample of stored telemetry, offered when building an Attribute Filter. A sample, not a schema — rare keys older than the sample are absent.
_Avoid_: key catalog, schema, known keys

**Severity Band**:
One of the four groups Bugler collapses the OTel severity scale into wherever it shows severity — Error (17 and above, so FATAL reads as an error), Warn, Info, and Debug (everything below 9, including telemetry that declared no severity at all). Coarser than the OTel levels on purpose: a band is what a colour means, so the same Log Record is the same colour in every view.
_Avoid_: severity level, severity class, log level

**Volume**:
How much telemetry a query matched over time rather than how much it matched in total: counts per Bucket across the Resolved Window, split by Severity Band. It answers the same Filter as the list it accompanies, so the two never describe different sets.
_Avoid_: histogram, distribution, counts, timeline

**Bucket**:
One slice of a Resolved Window that Volume is counted over. Their common width is chosen so a window yields a readable number of them, and their edges fall on multiples of that width, so a Bucket keeps its place while a Relative Range slides underneath. The first and last may be cut by the window rather than by their own width.
_Avoid_: bin, interval, slot, granularity

**Follow**:
Reading the log list as the end of the stream rather than as an answer: matching Log Records appear as they are stored, and the list is a window on what is arriving rather than an account of it — under load it shows the newest and passes over the rest without saying so. Leaving Follow replaces it with an ordinary query.
_Avoid_: live, tail, live tail, streaming, real-time

**Correlation**:
Navigating between a Log Record and the Trace it belongs to via their shared trace id.
_Avoid_: linking, join

**Visibility Scope**:
The set of Applications the current user may read; every Exploration query is constrained by it and it cannot be widened by request parameters.
_Avoid_: permissions, access list
