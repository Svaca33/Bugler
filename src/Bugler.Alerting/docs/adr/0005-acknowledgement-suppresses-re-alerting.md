---
status: accepted
---

# Acknowledgement suppresses re-alerting, and hands land only on the newest Episode

Acknowledged began as a label: who has taken this Episode on, surviving Quieting, carrying no
behaviour. That made it decorative at the moment it mattered most — the developer who said "I am
on it" kept being alerted, because the Episode quieted under them and the next recurrence opened
a new Episode and mailed everyone again. The mark now does what saying "I am on it" means:

- **An Acknowledged open Episode never Quiets.** It stays open, counting matches, until the
  verdict (Solve) or the mark's withdrawal. No closing, hence no new Episode of that kind, hence
  no new Alert — re-alerting is suppressed exactly while somebody holds the trouble.
- **All three hands — Acknowledge, Withdraw/Take-over, Solve — land only on the newest Episode
  of its kind** (Service + Fingerprint). Older Episodes are history; the API refuses with 409,
  which also nets the race where detection opens a newer Episode mid-click.
- **Acknowledging a closed (Quieted or Muted) newest Episode is allowed and is the mark alone** —
  nothing is held open, and a recurrence opens a new Episode that alerts as usual. The new
  Episode carries the context "an earlier Episode of this kind is acknowledged by X" as
  information, never as state: it is itself unacknowledged and quiets normally.
- **Solve consumes every acknowledgement its kind of trouble holds, on any of its Episodes.**
  The mark is a live claim, not a record; after the verdict, nothing claims the trouble is being
  worked on — and the "earlier acknowledged by" context therefore never reaches back past a
  Solve.
- **Sensitivity Off outranks the mark.** Muting closes Acknowledged Episodes too — with the
  watch off there is nothing to hold open — but leaves the mark visible on the closed Episode.

## Considered Options

- **Acknowledge writes "unlimited" into the kind's Quiet Window override** (the
  `FingerprintQuietWindow` of ADR 0004). Same suppression, rejected three times over: the mark
  would overwrite a hand-tuned window and Solve would have to restore a remembered value;
  anyone who may acknowledge would be mutating admin-tier configuration; and one concept would
  secretly serve two masters.
- **Carrying the acknowledgement over to the next Episode.** Rejected as fabrication: the mark
  asserts who acted and when, and an auto-copied mark asserts an act that never happened — worse,
  it would hold the new Episode open on the strength of a claim its holder never renewed. The
  informational "earlier acknowledged by X" line gives the same context without the lie.
- **Keeping Solve per-Episode.** Rejected: a verdict of "the cause was fixed" rendered on one
  historical stretch while the same trouble burns in a newer Episode is a statement nobody
  means. The verdict belongs to the kind's present, so it lands on the newest Episode only.

## Consequences

- ADR 0004 rejected an explicit "never quiets" *setting* because a permanently open Episode
  degrades the open-band reading. An Acknowledged open Episode is that permanently open Episode —
  deliberately: it is a named person's live, withdrawable claim shown with its holder, not
  anonymous configuration, and "somebody is sitting on live trouble" is a true reading. There is
  no time limit; the safety valves are visibility, Take-over, and Solve.
- History stops answering "who worked on it back then": Solve wipes the kind's marks without a
  tombstone, and older Episodes freeze without verdicts once a newer one exists. The Episode
  list answers "what needs a hand *now*", not attribution archaeology.
- The suppression is only as truthful as the holder. An acknowledged-and-forgotten Episode
  silently absorbs worse trouble of its kind — ADR 0001's accepted trade-off, taken on further,
  and the reason the mark stays loud in the UI while it lives.
