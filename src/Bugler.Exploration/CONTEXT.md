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
The criteria narrowing displayed telemetry: application, instance, tenant, severity, time range, full-text.
_Avoid_: query, search criteria

**Correlation**:
Navigating between a Log Record and the Trace it belongs to via their shared trace id.
_Avoid_: linking, join

**Visibility Scope**:
The set of Applications the current user may read; every Exploration query is constrained by it and it cannot be widened by request parameters.
_Avoid_: permissions, access list
