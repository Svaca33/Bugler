# Attribute filters use exact text equality, same-span EXISTS, and sampled key discovery

Attribute Filters compare the text representation of a value (`->>`/`#>>` extraction, case-sensitive equality), deliberately bypassing the GIN indexes on the `attributes` columns: GIN only accelerates typed containment (`@>`), which would force users to distinguish `5` from `"5"` when entering values. Text equality matches the pre-existing tenant filter, works identically for `attributes` and the unindexed `resource_attributes`, and keeps manual value entry frictionless. If volume ever demands it, expression indexes or a containment fast-path for string values can be added without changing the filter model.

Filter keys are carried as explicit path segments (scope + segment list), never parsed from dotted text — OTel keys themselves contain dots (`http.method` is one literal key), so a typed string like `a.b` is inherently ambiguous. Keys are therefore only ever selected structurally: from the detail-panel magnifier (which knows the real structure) or from a combobox of Observed Keys (computed on demand from a bounded sample of recent rows — chosen over an ingest-maintained key catalog to keep the write path free of read-side responsibilities).

On the traces list, a trace matches when a single span satisfies all Attribute Filters simultaneously (one EXISTS subquery), following Jaeger and TraceQL default semantics; "each filter satisfied by some span" was rejected as surprising — it can return traces containing no span that matches what the user described.

## Consequences

- Attribute Filter queries sequential-scan within the instance/time bounds; acceptable for self-hosted volumes, revisit if it hurts.
- A number-valued attribute is matched by its text form (`3` matches `retry.count = 3`).
- Keys absent from the recent sample cannot be offered in the combobox; the magnifier on a concrete record still works for them.
