# Alerting

The unattended watch over a Service: notices when one starts logging trouble or stops answering that it is alive, opens an Episode, and tells the people who asked. How a stretch of trouble ended — on its own, by a human verdict, or because the watching stopped — is read in the UI, never mailed. Detection is configured per Application; delivery is each person's own choice.

## Language

**Watch**:
What Bugler was looking at when it found the trouble and what keeps feeding it — the Log Records a Service sends, or the Health Check it answers. An Episode belongs to exactly one, each has its own switch, and a Fingerprint means something different under each: what tells one kind of trouble from another is the pair, never the Fingerprint alone.
_Avoid_: source, signal, detector, channel

**Health Check**:
The one address a Service may answer at to say it is alive, and the only place Bugler reaches outwards. Asked on the watch's own beat; a 2xx means alive and everything else — another status, a redirect, a refused connection, silence — means not. Never inherited from the Application, because every Service answers at its own address, and the address is the Watch's only switch: unset, nobody is asking.
_Avoid_: ping, heartbeat, liveness probe, uptime check

**Sensitivity**:
Which Severity Bands of a Service's Log Records may open or sustain an Episode — Off, Errors, or Errors and Warnings (bands as Exploration defines them). The Logs Watch's own setting and no other's: Off stops that watch and says nothing about whether anyone is still asking the Health Check. An Application-wide setting a Service may override; detection always reads its current value.
_Avoid_: alert level, threshold, trigger filter

**Quiet Window**:
The stretch of time an open Episode must go without a match before it closes. An Application-wide duration a Service may override, and one kind of trouble may override again — Episodes therefore fall quiet independently of one another.
_Avoid_: cooldown, resolve timeout, silence period

**Match**:
One observation of a kind of trouble: a Log Record at or above the Service's Sensitivity under the Logs Watch, one failed probe under the Health Check Watch. Matches open an Episode, feed it, and by ceasing let it fall quiet.
_Avoid_: hit, occurrence, sample

**Fingerprint**:
The kind of trouble a Match announces, distilled by the Application's Fingerprint Rule — under the Health Check Watch, the single reserved kind that is not answering. Opaque by design: it stands for the trouble rather than describing it, and the Title is what a person reads. What tells one Episode apart from another inside one Episode Scope, read together with the Watch it belongs to.
_Avoid_: error type, issue, grouping key

**Fingerprint Rule**:
How an Application's Fingerprints are distilled: by the throwing code, by the kind of failure, or by what was said — coarsening in that order — and optionally by one named attribute whose value, where a Match carries it, is the whole answer. An Application-wide setting no Service may override, because an Episode reaches across Services and they must agree on what "the same trouble" means. Versioned: the rule and the recipe behind it are stamped on every Episode, so a changed answer is legible rather than silent.
_Avoid_: grouping strategy, fingerprint config, matcher

**Title**:
The readable name of an Episode's trouble, taken once from its opening Match. Never an identity — two Episodes may share a Title and still be different troubles — so it may be as human as it likes.
_Avoid_: label, summary, name, description

**Runtime**:
Which language runtime a Match came from, as its sender declares it (OTel `telemetry.sdk.language`). Read, never registered — the only thing that says how a stack trace should be read, since the shape of one is each runtime's own affair. A Runtime Bugler cannot read leaves the Fingerprint Rule to coarsen one step, visibly.
_Avoid_: platform, language, environment, SDK

**Episode Scope**:
How far one Episode reaches: which facets of a sender — Service Namespace, Environment, Service Name — must match before two Matches of one kind share an Episode. An Application-wide setting; Environment stands by default, so the same trouble in two deployments of one Application meets in one Episode while production and staging never do. The Logs Watch's alone — a Health Check Episode is always its own Service's, because there the Service is what is being watched rather than where the trouble happened.
_Avoid_: grouping key, boundary, dimension

**Episode**:
One bounded stretch of one kind of trouble in one Episode Scope: opened by the first Match of a kind with no open Episode, counting every Match of that kind since. It ends by Quieting, by being Solved, or by being Muted — and never reopens: a later Match of the same kind starts a new Episode. Never spans Watches, never merges, and outlives the evidence that drove it.
_Avoid_: incident, outage, alert group, error burst

**Participation**:
What one Service running one version contributed to an Episode — when it first and last fell in, and how much. The answer to "is it still happening on the version we just shipped, and is it every deployment or only one". Held to a ceiling: past it the Matches are still counted, but no further Participation is opened.
_Avoid_: contributor, occurrence, source, involvement

**Quieted**:
How an unacknowledged Episode ends on its own: its Quiet Window passed without a Match. The trouble stopped; nothing is claimed to be fixed. Only the passage of time does this — no hand can, and an Acknowledged open Episode never does.
_Avoid_: auto-resolved, expired, closed by quiet window

**Solved**:
The one human verdict on a kind of trouble: the cause was fixed. Rendered only on the newest Episode of its kind and ends an open one on the spot. Terminal and irreversible — trouble that returns is a new Episode. Consumes every acknowledgement the kind of trouble holds, on any of its Episodes.
_Avoid_: resolved, fixed, closed

**Muted**:
How an Episode ends when what fed it is taken away — the Watch turned off (Sensitivity set to Off, a Health Check address cleared), or the Fingerprint Rule or Episode Scope changed under it, which leaves its kind of trouble in a partition nothing will report again. Either way the watching stopped and nothing is claimed about the problem. Reaches only that Watch's Episodes; the other watch's carry on. May still be Solved later.
_Avoid_: silenced, dismissed

**Acknowledged**:
The live mark that somebody has taken a kind of trouble on — who and when — placed only on its newest Episode. On an open Episode it also suppresses re-alerting: an Acknowledged Episode never Quiets, staying open and counting matches until Solved or withdrawn. On a closed one it is the visible mark alone. May be withdrawn or taken over by anyone who may see the Application, and landing on a machine-claimed Episode displaces the Machine Claim — the human hand always wins. Solve removes the kind's every acknowledgement — a Solved Episode is never Acknowledged. The mark says only what holds now; what happened stays in the Journal.
_Avoid_: assigned, claimed, owned, snoozed

**Machine Claim**:
A machine's visible, exclusive-among-machines hold on an Episode — a Machine Delegation saying "I am working on this" in its User's name. Laid only on an open Episode that is the newest of its kind and carries no Acknowledgement, no other machine's claim and no standing Resignation; while it stands the Episode never Quiets, the same hold an Acknowledgement exerts. A lease rather than a possession: it wilts unless a machine write renews it, and a lapsed claim falls off by itself with its Journal line — a crashed agent leaves a wilted mark, never a zombie Episode. The human hand always wins: an Acknowledgement displaces it, and anyone who may see the Application may withdraw it.
_Avoid_: lock, machine acknowledgement, assignment, ownership

**Machine Note**:
The one annotation the claim-holder may pin on its Episode — free text, a link, or both; pinning again replaces it. What was found, where the work lives; never a verdict.
_Avoid_: comment, machine comment, annotation thread

**Solved Proposal**:
The claim-holder's stated belief that the cause of an Episode's trouble is fixed, laid with the PR that fixes it. It ages visibly in Matches rather than minutes and never invalidates itself — a merged PR takes time to deploy, so the person renders the verdict with the count in view. Confirming it is the Solve itself, same verdict, same consequences; rejecting it removes the proposal and the claim with it. On an Episode no longer the newest of its kind it is overtaken — the trouble returned, the fix did not hold — and can no longer be confirmed.
_Avoid_: auto-solve, machine solve, suggested resolution, fix candidate

**Resignation**:
The machine hand's finding about itself: this trouble is not one it can fix, said with the reason why — a certificate that expired, a disk that filled, a third party that fails. Laying it ends the claim, tells the Episode's audience that a human hand is needed, and bars Machine Claims while it stands — but it says nothing about who acts next; that conclusion is a person's to draw. Cleared by an Acknowledgement, by the verdict, or by being swept aside by any human hand; a newer Episode of its kind leaves it overtaken, readable as history.
_Avoid_: handover, escalation, give-up, unsolvable flag, needs-human

**Journal**:
The append-only record of every hand laid on an Episode, and whether it was flesh or machine — Acknowledged, Withdrawn, Solved, and the machine hand's claims, notes, proposals and Resignations, each machine entry naming the delegation and through it its User. Entries are only ever added, never changed or removed; the live marks say what holds now, the Journal says what happened. When a Solve consumes an acknowledgement held by an earlier Episode of its kind, that Episode's Journal records the withdrawal by the solver's hand.
_Avoid_: audit log, history, event log, activity feed

**Archived**:
A closed Episode filed out of the everyday view — a mark laid on top of a state, never a state of its own. It says nothing about the trouble, because Quieted, Solved and Muted have already said it; any closed Episode may carry it, an open one may not, and lifting it restores the Episode unchanged. Shared rather than each reader's own: one Episode is filed for everyone, and the Journal records whose hand did it.
_Avoid_: deleted, resolved, closed, done, dismissed, hidden

**Deletion**:
The permanent removal of one kind of trouble from an Episode Scope — every Episode of it, and with them their Participations, Journals, Readings and the Deliveries still owed. Reaches the kind rather than one Episode, because what an Episode says about its kind — how often it recurred before, whose hand was laid on it earlier, whether a Solved Proposal has been overtaken — is an answer no surviving sibling could still give truthfully once one of them was gone. Only a kind whose every Episode is closed and Archived may be Deleted, and only by an Admin: it takes the Journal with it, which nothing else in Alerting may do.
_Avoid_: archive, hide, purge, prune

**Alert**:
The message announcing that an Episode has begun to concern someone: that it opened — which Service, when, and the opening Match itself, the Log Record or what the probe got back — or that a Service they follow has just fallen into one already running, which says since when instead. Exactly one per Episode per recipient, so following both an Application and one of its Services is told once.
_Avoid_: notification, alarm

**Storm**:
More kinds of trouble opening in one Episode Scope at once than anybody can read. The Episodes open unhindered and every one of them is there to be seen — it is the Alerts that fold into a single message naming how many, because a hundred mails bury the one that mattered. A Storm is a sender's grouping gone wrong as often as it is a real outage, and saying so is what lets somebody go and coarsen the Fingerprint Rule.
_Avoid_: flood, burst, rate limit, throttle

**Reading**:
The machine's reading of an Episode's opening evidence — two or three sentences on what is likely going on, written once as the Episode opens, in every language Bugler speaks. It stands beside the evidence and never above it: visibly machine-made, and no verdict rests on it — Solved stays a human's alone. An Episode has at most one, and only when the server has an AI provider and the Application has consented; trouble that returns gets a fresh one with its new Episode.
_Avoid_: triage, summary, analysis, explanation, AI insight

**Subscription**:
A User's standing personal request to be mailed Alerts — for one Application (all its Services, present and future) or for one Service. Dormant while its User cannot currently read the Application; dies only with the Deletion of the User or of what it points at.
_Avoid_: watch, recipient list, notification preference

**Chat Webhook**:
The one optional Google Chat incoming webhook an Application may hold; every Alert of its Services also goes there. A secret only Admins handle.
_Avoid_: chat integration, space URL

**Delivery**:
One message owed to one recipient over one channel — mail to a subscribed User, or the Application's Chat Webhook — pursued until it succeeds or its time runs out, because a stale Alert wakes more panic than none.
_Avoid_: send attempt, outbox message, notification record
