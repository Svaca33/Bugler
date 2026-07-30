---
status: accepted
---

# Episodes group by kind of trouble within a Service

ADR 0001 shipped with one Episode per Service: any matching Log Record fed the Service's single
open Episode. In use that hid exactly what the accepted trade-off predicted — a steady trickle of
one known error held the Episode open forever, and a *different* kind of trouble in the same
Service raised a counter nobody was watching instead of an Alert. The product owner's expectation
was per-kind grouping all along, so the model now matches it: an Episode belongs to a
(Service, **Fingerprint**) pair, at most one open per pair. The payment-decline trickle keeps its
own Episode open; a warehouse timeout opens — and, after its Quiet Window, resolves — its own.

The Fingerprint is a heuristic, and that is the real cost of this decision. The sender's message
template is used when it travels with the record (`{OriginalFormat}` as .NET loggers send it, or
`event.name`), which groups exactly. Otherwise the body stands in, with uuids, hex runs, and
numbers blanked — which can split one kind into several (variable words survive normalization:
"insufficient funds" vs "card expired" are different Fingerprints without a template) and merge
kinds that share a template. Both failure modes are accepted; refining the normalizer is cheap
and local. Against pathological bodies that defeat it entirely, a Service caps its open Episodes
(25) and further kinds fold into one overflow Episode — degraded grouping, never an alert flood.

## Consequences

- One Service outage can now announce several Episodes — bounded by its distinct kinds of
  trouble, not by its log volume. That is the point, not a regression: each Alert says something
  new, which ADR 0001 already held to be information rather than spam.
- The one-open invariant index became `(service_id, fingerprint) WHERE closed_at IS NULL`;
  Episodes carry the Fingerprint, and detection accumulates per pair.
- Episodes from before this decision carry an empty Fingerprint; new matches never join them, so
  they drain through their Quiet Windows and close on their own.
- The detection poll now reads the two grouping attributes alongside the body; composition and
  delivery are untouched — an Alert already carries the first Log Record, which names the kind.
