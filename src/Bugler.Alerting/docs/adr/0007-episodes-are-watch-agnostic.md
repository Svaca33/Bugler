---
status: accepted, amended by ADR 0011 (the Watch joined every kind's key)
---

# Episodes belong to a Watch rather than to the log stream

Everything the Episode is made of assumed a Log Record had opened it: it required a first log id,
timestamp, severity and body, its Fingerprint was defined as "the kind of trouble a Log Record
announces", and the only thing that could feed it was the detection poll over
`telemetry.log_records`. That was true while there was one way to find trouble. It stopped being
true the moment a second one arrived — a health check that nothing logs — and it will be wrong
again when metrics land.

So the Episode names its **Watch**, and what it carries is the opening **Match** rather than the
opening log: `first_match_log_id` (null where the Watch points at no Log Record),
`first_match_at`, `first_match_severity` (null where the Watch has no Severity Bands),
`first_match_detail`. `EpisodeCloseReason.SensitivityOff` became `WatchOff`, because Sensitivity is
one Watch's switch and Muting is every Watch's ending.

Two alternatives were weighed and dropped. **A separate aggregate for each new kind of trouble**
would have duplicated Acknowledged, Solved, the Journal, Subscriptions, Deliveries, the Chat
Webhook, the Episodes page with its filters and counts, and the Dashboard tile — all of which
apply to "the health check is failing" word for word. **A generic evidence payload** (a side table
or a JSON column) buys flexibility that three shapes do not need and costs every read site its
types; the message composer, the detail panel and the service card would all be deserialising
something unnamed.

What the Watch is *not* is a second Fingerprint. It is the space a Fingerprint is read in: the
reserved kind `(health check failing)` and a log whose body happens to read the same way are
different trouble, which is why the one-open-per-kind index became
`(service_id, watch, fingerprint) WHERE closed_at IS NULL`.

## Consequences

- A Watch enum value is now part of the durable model. Adding one — metrics — is a migration-free
  change to detection plus whatever settings that watch needs; the Episode, the Alert, the
  Journal and the whole UI take it as it stands.
- Every read site must cope with an Episode that quotes no log: no severity badge, no "open in
  logs" link, no error and warning counts. Those are branches on the Watch, not on nullability,
  so a third Watch cannot silently fall into the Logs rendering.
- The rename touched the public shapes the frontend consumes (`firstLog*` → `firstMatch*`). Cheap
  now, before 1.0, and the reason the change shipped on its own ahead of the feature that needed
  it: with nothing else in the commit, "nothing changed" was the whole thing to verify.
- Recurrence and the grouped list still index on `(service_id, fingerprint)` without the Watch.
  Fingerprints do not collide across Watches in practice, so the counts are the same; only the
  invariant needed to be exact.
