---
status: accepted
---

# The Journal keeps what the live marks let go

ADR 0005 made the acknowledgement a live claim, not a record, and accepted the consequence:
"History stops answering 'who worked on it back then'." In practice that consequence surfaced
as a hole in the Episode detail — the Lifecycle section renders from the live columns, so a
withdrawal or a Solve erased lines a reader had already seen. The facts were not merely
unshown; they were gone.

This decision amends that consequence of ADR 0005 (the document itself stands as written).
The live marks keep their exact semantics — one slot, last hand wins, Solve consumes the
kind's every acknowledgement — and a **Journal** now stands beside them: an append-only
record of every human hand laid on an Episode.

- **Three entry kinds — Acknowledged, Withdrawn, Solved** — each carrying the actor and the
  moment. A take-over is not a fourth kind: "A acknowledged" followed by "B acknowledged"
  with no withdrawal between them *is* the take-over, and the UI narrates it from the
  sequence. A second kind would be a second source of truth about what the order already says.
- **Withdrawn carries who withdrew.** Before the Journal, the withdrawing hand was discarded
  before it was ever recorded — anyone within the Visibility Scope may withdraw anyone's mark,
  and nothing said whose hand it was.
- **When Solve consumes an acknowledgement held by an earlier Episode of its kind, that
  Episode's Journal records a Withdrawn entry by the solver's hand**, timestamped at the
  Solve. Every Journal is complete on its own: no mark vanishes without an entry saying who
  ended it and when. The entry carries no "consumed by Solve" flag — the shared timestamp
  with the newer Episode's Solved entry tells that story.
- **Only acts are written.** Withdrawing nothing writes nothing, and a re-acknowledgement by
  the current holder is now a true no-op on both sides — the live timestamp is no longer
  refreshed, so the live mark never shows a moment the Journal cannot explain. Refused hands
  (409s) write nothing.
- **Machine facts stay out.** Opened, Quieted, Muted, and the Alert deliveries are already
  durable columns and rows; the Lifecycle view merges them with the Journal chronologically.
  Journaling them too would store the same fact twice, against ADR 0003's "derived, never
  stored twice".
- **Existing live marks are backfilled** at migration: a live acknowledgement becomes an
  Acknowledged entry, a Solved Episode gets a Solved entry, from the columns already held.
  Marks withdrawn or consumed before the Journal existed are unrecoverable and stay lost.
  After the backfill the UI reads human acts from the Journal alone — no second code path
  for pre-Journal Episodes.

## Considered Options

- **Changing the live-mark semantics instead** — making withdrawal and take-over keep the old
  mark somehow. Rejected: the live claim's behaviour (suppressing re-alerting, one holder,
  consumed by the verdict) is settled and correct; the loss was informational, not
  behavioural. Recording belongs beside the state, not inside it.
- **Journaling everything, machine transitions included**, for one uniform timeline source.
  Rejected: Opened/Quieted/Muted and deliveries are already durable and would be stored
  twice. Only human acts can be lost, so only human acts are journaled.
- **Deriving history from nothing** — keeping the panel as a projection of live columns and
  accepting the erasure. Rejected: that is the status quo this decision exists to end.

## Consequences

- ADR 0005's consequence "History stops answering 'who worked on it back then'" no longer
  holds: the Journal answers it. The mark is still a live claim; what happened is now the
  Journal's to tell.
- The Journal is strictly write-once: entries are never edited or deleted while the Episode
  lives, and they die with it. It records hands, not outcomes — whether the claim "helped"
  is not its business.
- A re-acknowledgement by the current holder no longer refreshes the mark's timestamp — the
  one behavioural change to the live marks, made so "not an act" holds on both sides.
