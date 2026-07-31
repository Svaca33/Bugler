---
status: accepted
---

# Human verdicts join the Episode lifecycle; the All Clear retires

An Episode's life was machine-owned end to end: opened by the first match, closed when its
Quiet Window passed or its Sensitivity turned Off. "Closed" said only that the trouble stopped —
never that anyone looked, nor that anything was fixed. Falling quiet and being fixed are
different facts, so they now live in different places, and neither may masquerade as the other:

- The **machine** owns how a stretch ends: **Quieted** (the Quiet Window passed) and **Muted**
  (Sensitivity turned Off) — the close reasons that already existed — joined by **Solved** when
  a human closes by hand.
- **Humans** own two marks. **Acknowledged** is a live flag (who, when) meaning somebody has
  taken the Episode on — deliberately *not* a lifecycle state, because the closing sweep must
  never overwrite a human's claim nor race their clicks; it survives Quieting, can be withdrawn
  or taken over, and is consumed by Solve. **Solved** is the terminal verdict "the cause was
  fixed": allowed on any not-yet-Solved Episode, closing an open one on the spot, irreversible.

A closed Episode never reopens. A match after Quieted or Solved opens a new Episode —
recurrence is news, and a premature Solve earning an instant fresh Alert is honest feedback,
not spam. Continuity ("this burned before, and who solved it") is a read-side concern: the UI
groups the Episodes of one (Service, Fingerprint) pair; it does not stitch their identities.

The **All Clear retires entirely**, amending ADR 0001's "Both Alert and All Clear are always
sent". The Alert is the only actionable message; how a stretch ended is read in the UI next to
the Fingerprint's history. Quieting and solving therefore notify nobody — and Acknowledged
never did.

## Considered options

- **A single state enum including Acknowledged** (the issue's original sketch) — rejected: it
  collapses machine facts and human facts into one column, the very conflation this change
  exists to undo. The scheduler would erase Acknowledged at quieting, and the closing sweep
  would race the Solve click over one field.
- **Reviving a Quieted Episode on recurrence** — rejected: it breaks the bounded-stretch
  identity, and must either re-alert (breaking "exactly one Alert per Episode per channel") or
  recur in silence.
- **Gating Solve on Quieted** ("no claiming fixed while it still burns") — rejected: nobody
  returns after the Quiet Window to file the verdict, so Solved would go unused.

## Consequences

- Alert recipients are never told how the story ended; the ending lives only in the UI. That is
  the deliberate trade: the Alert is the sole actionable message, everything else was noise.
- `EpisodeCloseReason` is not absorbed by a state column: it survives as "how the stretch
  ended" (QuietWindow, SensitivityOff, Solved), with the verdict and the flag as columns beside
  it — so quieted-then-Solved stays distinguishable from Solved-while-open, and the display
  state is derived, never stored twice.
- Solve wipes the acknowledgement. Acknowledged is a live claim, not an audit trail; after
  Solve only the solver's name remains.
- Anyone with read visibility of the Application may acknowledge, take over, un-acknowledge,
  and solve — they are exactly the audience the grants already chose. No new grant kind.
