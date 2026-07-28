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
The criteria narrowing displayed telemetry: Source Filters, severity, time range, full-text, and Attribute Filters. Tenant is not a first-class criterion — it is filtered via an Attribute Filter on `tenant.id`.
_Avoid_: query, search criteria

**Source Filter**:
A Filter criterion narrowing which Services a query may touch — Application, Service Namespace, Environment or Service Name — each one left open meaning "all". Unlike an Attribute Filter it matches registered facts, so it cannot be fooled by what telemetry claims about itself.
_Avoid_: scope filter, instance filter, source picker

**Attribute Filter**:
A Filter criterion matching one attribute — identified by its scope (signal attribute vs Resource Attribute) and its path — against an exact value. Combined with AND across keys; one value per key at a time.
_Avoid_: property filter, tag filter, key-value filter

**Resource Attribute**:
An attribute describing the entity that emitted the telemetry (service, host, deployment) rather than the individual Log Record or Span.
_Avoid_: tags, metadata, resource field

**Observed Keys**:
The attribute keys present in a recent sample of stored telemetry, offered when building an Attribute Filter. A sample, not a schema — rare keys older than the sample are absent.
_Avoid_: key catalog, schema, known keys

**Correlation**:
Navigating between a Log Record and the Trace it belongs to via their shared trace id.
_Avoid_: linking, join

**Visibility Scope**:
The set of Applications the current user may read; every Exploration query is constrained by it and it cannot be widened by request parameters.
_Avoid_: permissions, access list
