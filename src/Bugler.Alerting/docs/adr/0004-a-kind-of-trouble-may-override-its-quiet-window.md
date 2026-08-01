---
status: accepted
---

# A kind of trouble may override its Quiet Window

ADR 0002 gave every Fingerprint of a Service its own Episode, but left the Quiet Window resolving
per Service: fifteen minutes of silence closes whatever is open. For a known error that recurs
every twenty minutes that is exactly wrong — each recurrence is a new Episode, and an Episode
opening is an Alert, so one familiar fault mails its subscribers all day. The Quiet Window now
resolves one tier deeper: `kind of trouble ?? Service ?? Application ?? default`, stored as
`alerting.fingerprint_quiet_windows` keyed on `(service_id, fingerprint)` and capped at seven
days. Stretch the window past the fault's cadence and the recurrences feed one open Episode
again — one Alert, not twelve.

This is not the cooldown ADR 0001 rejected. A cooldown suppresses messages about an Episode the
model believes is over, and so loses the notion of recovery; a longer Quiet Window keeps the
Episode **open**, so the model still holds that the trouble is ongoing, the counters keep
climbing, and the "open now" reading of the system stays true. ADR 0003 having retired the All
Clear, a wider window costs no message at all — it only removes Alerts that said nothing new.

The override belongs to the pair, not to the Episode. Episodes never reopen (ADR 0001), so a
window that died with its Episode would be spent on trouble that has already stopped, and would
leave the *next* recurrence to mail as before — the case this exists for. It therefore survives
its Episode, and any Episode of that kind — open, Quieted, Solved or Muted — can set it: by the
time the mails have become annoying, every Episode of the annoying kind is usually closed.
Setting it from a closed Episode changes nothing visible until the next one, which the UI has to
say out loud, because "this will reopen it" is the obvious wrong guess.

Only the Quiet Window descends this far, not Sensitivity. Detection decides whether a Log Record
matches by comparing its severity to the Service's floor *before* it computes the Fingerprint
(`DetectionBatch`), so a per-kind Sensitivity would mean a differently shaped detection pass, not
a nullable column; and turning Sensitivity Off closes Episodes as Muted through machinery that
works in whole Services (`SilentClose`, the deletion cascades). That is a separate feature with a
separate design, and the table's name — `FingerprintQuietWindow`, not `…AlertingSettings` —
promises no more than it does.

## Considered Options

- **Per-Episode override.** Simpler by a table: a column on `episodes`, no new tier, no glossary
  change. Rejected because it cannot stop the repeated Alerts — it only prevents one long
  Episode from splitting.
- **Per-Application Fingerprint override.** Would tune the same noisy error across every
  environment at once. Rejected on ADR 0001's ground: dev noise must not silence production, and
  a Service's Namespace/Environment/Name identity is what makes those separate Services.
- **An explicit "never quiets" value.** The honest spelling of "I know about this one". Rejected
  because a permanently open Episode lights the dashboard's open band forever, degrading the
  reading the tool exists to give; seven days is the point past which Sensitivity Off, or a fix,
  is the right answer.

## Consequences

- The Quiet Window is capped only on this tier. Application and Service windows keep their
  `>= 1` validation and no upper bound — a deliberate asymmetry: nobody types a year
  Application-wide by accident, but they might on the error that woke them at 03:00.
- `EffectiveSettings` now resolves per `(Service, Fingerprint)` and is always built whole, so
  `EpisodeDetector` loads a table it never reads from. One shared path is worth the query.
- A tuned kind holds its Episode open across recurrences, so worse trouble *of that same kind*
  inside the open Episode goes unannounced for longer — ADR 0001's accepted trade-off, taken
  more of on purpose.
- Episodes from before ADR 0002 carry an empty Fingerprint and are refused a window: they take
  no new matches, so it would be configuration for trouble that cannot recur.
- Deleting a Service or an Application drops its tuned kinds along with its Episodes; every
  override is therefore always reachable from at least one Episode, and none can be orphaned.
