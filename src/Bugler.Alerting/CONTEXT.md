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
The stretch of time an open Episode must go without a match before it closes. An Application-wide duration a Service may override, and one kind of trouble in a Service may override again — a Service's Episodes therefore fall quiet independently of one another.
_Avoid_: cooldown, resolve timeout, silence period

**Match**:
One observation of a kind of trouble: a Log Record at or above the Service's Sensitivity under the Logs Watch, one failed probe under the Health Check Watch. Matches open an Episode, feed it, and by ceasing let it fall quiet.
_Avoid_: hit, occurrence, sample

**Fingerprint**:
The kind of trouble a Match announces — under the Logs Watch, the sender's message template when it travels along, otherwise the body with its variable parts blanked; under the Health Check Watch, the single reserved kind that is not answering. What tells one Episode of a Service apart from another, read together with the Watch it belongs to.
_Avoid_: error type, issue, grouping key

**Episode**:
One bounded stretch of one kind of trouble in one Service: opened by the first Match of a kind with no open Episode, counting every Match of that kind since. It ends by Quieting, by being Solved, or by being Muted — and never reopens: a later Match of the same kind starts a new Episode. Never spans Services, never spans Watches, never merges, and outlives the evidence that drove it.
_Avoid_: incident, outage, alert group, error burst

**Quieted**:
How an unacknowledged Episode ends on its own: its Quiet Window passed without a Match. The trouble stopped; nothing is claimed to be fixed. Only the passage of time does this — no hand can, and an Acknowledged open Episode never does.
_Avoid_: auto-resolved, expired, closed by quiet window

**Solved**:
The one human verdict on a kind of trouble: the cause was fixed. Rendered only on the newest Episode of its kind and ends an open one on the spot. Terminal and irreversible — trouble that returns is a new Episode. Consumes every acknowledgement the kind of trouble holds, on any of its Episodes.
_Avoid_: resolved, fixed, closed

**Muted**:
How an Episode ends when the Watch feeding it is turned off — Sensitivity set to Off, or a Health Check address cleared: the watching stopped, nothing is claimed about the problem. Reaches only that Watch's Episodes; the other watch's carry on. May still be Solved later.
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

**Alert**:
The message announcing that an Episode opened: which Service, when, and the opening Match itself — the Log Record, or what the probe got back. Exactly one per Episode per channel.
_Avoid_: notification, alarm

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
