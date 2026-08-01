# Alerting

The unattended watch over incoming logs: notices when a Service starts logging trouble, opens an Episode, and tells the people who asked. How a stretch of trouble ended — on its own, by a human verdict, or because the watching stopped — is read in the UI, never mailed. Detection is configured per Application; delivery is each person's own choice.

## Language

**Sensitivity**:
Which Severity Bands of a Service's Log Records may open or sustain an Episode — Off, Errors, or Errors and Warnings (bands as Exploration defines them). An Application-wide setting a Service may override; detection always reads its current value.
_Avoid_: alert level, threshold, trigger filter

**Quiet Window**:
The stretch of time an open Episode must go without a matching Log Record before it closes. An Application-wide duration a Service may override, and one kind of trouble in a Service may override again — a Service's Episodes therefore fall quiet independently of one another.
_Avoid_: cooldown, resolve timeout, silence period

**Fingerprint**:
The kind of trouble a Log Record announces: the sender's message template when it travels along, otherwise the body with its variable parts blanked. What tells one Episode of a Service apart from another.
_Avoid_: error type, issue, grouping key

**Episode**:
One bounded stretch of one kind of trouble in one Service: opened by the first matching Log Record of a Fingerprint with no open Episode, counting every match of that Fingerprint since. It ends by Quieting, by being Solved, or by being Muted — and never reopens: a later match of the same Fingerprint starts a new Episode. Never spans Services, never merges, and outlives the Log Records that drove it.
_Avoid_: incident, outage, alert group, error burst

**Quieted**:
How an unacknowledged Episode ends on its own: its Quiet Window passed without a matching Log Record. The trouble stopped; nothing is claimed to be fixed. Only the passage of time does this — no hand can, and an Acknowledged open Episode never does.
_Avoid_: auto-resolved, expired, closed by quiet window

**Solved**:
The one human verdict on a kind of trouble: the cause was fixed. Rendered only on the newest Episode of its kind and ends an open one on the spot. Terminal and irreversible — trouble that returns is a new Episode. Consumes every acknowledgement the kind of trouble holds, on any of its Episodes.
_Avoid_: resolved, fixed, closed

**Muted**:
How an Episode ends when its Service's Sensitivity turns Off: the watching stopped, nothing is claimed about the problem. May still be Solved later.
_Avoid_: silenced, dismissed

**Acknowledged**:
The live mark that somebody has taken a kind of trouble on — who and when — placed only on its newest Episode. On an open Episode it also suppresses re-alerting: an Acknowledged Episode never Quiets, staying open and counting matches until Solved or withdrawn. On a closed one it is the visible mark alone. May be withdrawn or taken over by anyone who may see the Application; Solve removes the kind's every acknowledgement — a Solved Episode is never Acknowledged, and no record of past marks remains.
_Avoid_: assigned, claimed, owned, snoozed

**Alert**:
The message announcing that an Episode opened: which Service, when, and the first matching Log Record itself. Exactly one per Episode per channel.
_Avoid_: notification, alarm

**Subscription**:
A User's standing personal request to be mailed Alerts — for one Application (all its Services, present and future) or for one Service. Dormant while its User cannot currently read the Application; dies only with the Deletion of the User or of what it points at.
_Avoid_: watch, recipient list, notification preference

**Chat Webhook**:
The one optional Google Chat incoming webhook an Application may hold; every Alert of its Services also goes there. A secret only Admins handle.
_Avoid_: chat integration, space URL

**Delivery**:
One message owed to one recipient over one channel — mail to a subscribed User, or the Application's Chat Webhook — pursued until it succeeds or its time runs out, because a stale Alert wakes more panic than none.
_Avoid_: send attempt, outbox message, notification record
