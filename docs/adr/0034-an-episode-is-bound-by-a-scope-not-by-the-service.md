---
status: accepted
---

# An Episode is bound by a configurable Scope, not by the Service

An Episode was one stretch of one kind of trouble in **one Service**, and a Service is one role of one deployment (ADR 0006). An Application installed once per customer therefore has one Service per customer per role, and the same bug in the same code reaches a person as several Episodes — one per tenant, each with its own Alert, its own acknowledgement and its own verdict, all describing a single thing to fix. The split follows the deployment topology rather than the trouble.

So an Episode is bound by an **Episode Scope**: the Application, plus whichever facets of the sender an admin says must match — Service Namespace, Environment, Service Name. Environment stands by default and the other two do not, so the same trouble in two deployments of one Application meets in one Episode while production and staging never do. What the Episode then keeps is a **Participation** per Service and version that fell into it, with its own first and last sighting and its own tally — the answer to "is it still happening on the version we just shipped, and is it every deployment or only one".

**Environment is the default boundary, and deliberately so.** Merging it is offered and not recommended. Staging and production share their code and therefore their Fingerprints, so a merged Episode cannot say which of them is on fire, and — worse — a test run that keeps failing feeds the Episode indefinitely, so the Quiet Window never elapses and the production trouble never falls quiet. Solved compounds it: one verdict would cover both, though the fix has reached only one of them.

**The Scope governs the Logs Watch alone.** A Health Check Episode is always its own Service's. Its Fingerprint is a single reserved kind, so it discriminates nothing on its own: under an Application-wide Scope, a backend that stopped answering and a mobile API that stopped answering would be one Episode. Under the Logs Watch a Service is *where* the trouble happened and is properly a facet; under the Health Check Watch a Service is *what is being watched*, and there is nothing to fold.

**The Scope is a setting, not a new entity.** ADR 0006 considered a Deployment level between Application and Service and rejected it as premature; it still is. Normalising namespace and environment out of the Service would move every Service's registration, every filter and every grant, to serve a question that a per-Application setting answers on its own.

## Considered Options

- **Episodes per Application, flat.** The original request, and rejected on its author's own counter-example: a backend and a mobile client are one Application and must never share an Episode. The Fingerprint does separate them in practice — different code, different frames — but the Health Check Watch's reserved Fingerprint does not, and neither answer distinguishes production from staging.
- **Adding Service Namespace and Environment to the Fingerprint.** The same effect through the wrong door. A Fingerprint answers *what happened*; a namespace answers *where*. Mixing them would have made Quiet Window overrides per-tenant, changed what "the newest Episode of its kind" means for Solved, and put a customer's name inside the identity of an error.
- **Keeping the cap on open Episodes per Scope.** A Service could hold 25 open kinds of trouble before the rest were folded into one overflow Episode. With Fingerprints as fine as ADR 0033 makes them, and with one Scope now covering what used to be ten Services' worth of budget, that cap would hide real distinct failures inside a bucket — reinventing the mixed Episode this work exists to remove. So the cap moves off Episodes and onto Alerts: Episodes open without limit and all of them are visible, while a Storm folds their Alerts into one message. What the cap really guarded was the mailbox, not the table.

## Consequences

- An Episode has no single Service. It keeps `OpenedByServiceId` — the Service whose Match opened it, which the opening evidence belongs to anyway — but that is part of the evidence, not an owner. Filtering Episodes by Service goes through the Participations.
- The one-open-Episode invariant is enforced on the Scope key rather than the Service, and a Health Check Episode's Scope key is simply its Service. Quiet Window overrides are re-keyed the same way.
- **An Alert is owed once per Episode per recipient, not once per channel.** A Service that falls into a running Episode owes an Alert to its own followers who have not already had one — otherwise somebody following only their own tenant would never hear that their tenant is affected, which is precisely what a per-Service Subscription is for. That late message says since when the Episode has been running rather than announcing an opening, because for its recipient nothing opened just now.
- Participations are held to a ceiling. A sender that puts a build id into `service.version` would otherwise open one per process; past the ceiling the Matches still count and no further Participation is opened.
- The version on a Participation is read from the Match's own `service.version`, not from `telemetry.releases`. That ledger is deliberately imprecise — it holds stragglers for thirty minutes and passes over quick rollbacks (ADR 0016) — and during a rolling deploy it reports one version while two are demonstrably running, which is the exact moment this question gets asked.
- Episodes are never purged, and without a cap a pathologically grouped sender can grow the table without bound. The Storm digest is the first warning that it is happening, and the Fingerprint Rule (ADR 0033) is the remedy that did not exist when the cap was written.
- On upgrade, open Logs Episodes are Muted rather than left to quiet out: their Fingerprints belong to a partition that no longer exists, and an Acknowledged Episode would otherwise never close at all. Live acknowledgements and machine claims fall with them, the Journals keep what happened, and a trouble solved last week returns as a new Episode when it next occurs.
