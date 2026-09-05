---
status: accepted
---

# A kind of trouble is told by its Watch everywhere, not only where it is open

Alerting ADR 0007 settled what a kind of trouble is — the Watch and the Fingerprint together
inside an Episode Scope — and then keyed only the one-open-per-kind index that way, closing with
the admission that recurrence and the grouped list still asked on the Scope and Fingerprint alone.
The reason it was safe is written into `EpisodeScope`: the Logs Watch builds its key as `app=…`
and the Health Check Watch as `service=…`, so no Scope key has ever held both Watches and the
Watch was implied by the string. Every one of those answers was therefore right.

It was right by accident. A third Watch — metrics, the one Alerting ADR 0007 says the model is
shaped for — would key its Episodes somehow, and the first time two Watches shared a Scope key the
wrong answers would arrive silently: an Acknowledgement refused because another Watch's Episode
is "newer", a Solve consuming a stranger's marks, a recurrence count inflated by trouble nobody
was looking at.
Nothing would fail; the numbers would just stop meaning what they say.

So the thirteen cross-Episode queries that identify a kind — the newest-of-kind gate on the human
hands and on the machine's, the Solve sweep over a kind's acknowledgements, the recurrence count
and the earlier-acknowledgement hint on both the list and the detail, the overtaken flag on Solved
Proposals and Resignations, the rail's standing counts, and the grouped list's choice of face —
all key on Scope + Watch + Fingerprint. The `kind_history` index gains the Watch so they still read
one index, and so does the primary key of `fingerprint_quiet_windows`, whose own doc comment
already claimed to be "the pair an Episode is told apart by" while being one column short of it.

Nothing observable moves, and that is the point: this is stated ahead of the features that will
rest on it — an irreversible Deletion reaching a whole kind (#42) most of all — rather than
underneath them.

## Consequences

- Alerting ADR 0007's last consequence is retired. The prefix accident is used exactly once more,
  in the migration that backfills each existing Quiet Window override's Watch from its Scope key,
  and never again.
- A Quiet Window override is now addressable per Watch. No UI offers that yet — the endpoint takes
  the Watch from the Episode it is addressed through — and none needs to; the key simply no longer
  lies about what it holds.
- A third Watch may key its Episode Scope however suits it. That was already true of the invariant
  and is now true of every question asked about a kind.
