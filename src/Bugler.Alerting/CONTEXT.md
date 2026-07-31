# Alerting

The unattended watch over incoming logs: notices when a Service starts logging trouble, opens an Episode, and tells the people who asked. How a stretch of trouble ended — on its own, by a human verdict, or because the watching stopped — is read in the UI, never mailed. Detection is configured per Application; delivery is each person's own choice.

## Language

**Sensitivity**:
Which Severity Bands of a Service's Log Records may open or sustain an Episode — Off, Errors, or Errors and Warnings (bands as Exploration defines them). An Application-wide setting a Service may override; detection always reads its current value.
_Avoid_: alert level, threshold, trigger filter

**Quiet Window**:
The stretch of time a Service must go without a matching Log Record before its open Episode closes. An Application-wide duration a Service may override.
_Avoid_: cooldown, resolve timeout, silence period

**Fingerprint**:
The kind of trouble a Log Record announces: the sender's message template when it travels along, otherwise the body with its variable parts blanked. What tells one Episode of a Service apart from another.
_Avoid_: error type, issue, grouping key

**Episode**:
One bounded stretch of one kind of trouble in one Service: opened by the first matching Log Record of a Fingerprint with no open Episode, counting every match of that Fingerprint since. It ends by Quieting, by being Solved, or by being Muted — and never reopens: a later match of the same Fingerprint starts a new Episode. Never spans Services, never merges, and outlives the Log Records that drove it.
_Avoid_: incident, outage, alert group, error burst

**Quieted**:
How an Episode ends on its own: its Service's Quiet Window passed without a matching Log Record. The trouble stopped; nothing is claimed to be fixed. Only the passage of time does this — no hand can.
_Avoid_: auto-resolved, expired, closed by quiet window

**Solved**:
The one human verdict on an Episode: the cause was fixed. May be rendered on any Episode not yet Solved and ends an open one on the spot. Terminal and irreversible — trouble that returns is a new Episode. Consumes any acknowledgement.
_Avoid_: resolved, fixed, closed

**Muted**:
How an Episode ends when its Service's Sensitivity turns Off: the watching stopped, nothing is claimed about the problem. May still be Solved later.
_Avoid_: silenced, dismissed

**Acknowledged**:
The live mark that somebody has taken an Episode on — who and when. A flag beside the lifecycle, not a state of it: it survives Quieting, may be withdrawn or taken over by anyone who may see the Application, and is removed by Solve — a Solved Episode is never Acknowledged.
_Avoid_: assigned, claimed, owned

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
