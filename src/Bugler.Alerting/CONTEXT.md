# Alerting

The unattended watch over incoming logs: notices when a Service starts logging trouble, opens an Episode, tells the people who asked, and tells them when it is over. Detection is configured per Application; delivery is each person's own choice.

## Language

**Sensitivity**:
Which Severity Bands of a Service's Log Records may open or sustain an Episode — Off, Errors, or Errors and Warnings (bands as Exploration defines them). An Application-wide setting a Service may override; detection always reads its current value.
_Avoid_: alert level, threshold, trigger filter

**Quiet Window**:
The stretch of time a Service must go without a matching Log Record before its open Episode closes. An Application-wide duration a Service may override.
_Avoid_: cooldown, resolve timeout, silence period

**Episode**:
One bounded stretch of trouble in one Service: opened by the first Log Record its Sensitivity matches, counting every match since, closed when a Quiet Window passes without one — or immediately and silently when Sensitivity turns Off. Never spans Services, never merges, and outlives the Log Records that drove it.
_Avoid_: incident, outage, alert group, error burst

**Alert**:
The message announcing that an Episode opened: which Service, when, and the first matching Log Record itself. Exactly one per Episode per channel.
_Avoid_: notification, alarm

**All Clear**:
The message announcing that an Episode closed by falling quiet: how long it ran and how much it counted. An Episode silenced by Sensitivity turning Off sends none — nothing was resolved.
_Avoid_: resolved notice, recovery message

**Subscription**:
A User's standing personal request to be mailed Alerts and All Clears — for one Application (all its Services, present and future) or for one Service. Dormant while its User cannot currently read the Application; dies only with the Deletion of the User or of what it points at.
_Avoid_: watch, recipient list, notification preference

**Chat Webhook**:
The one optional Google Chat incoming webhook an Application may hold; every Alert and All Clear of its Services also goes there. A secret only Admins handle.
_Avoid_: chat integration, space URL

**Delivery**:
One message owed to one recipient over one channel — mail to a subscribed User, or the Application's Chat Webhook — pursued until it succeeds or its time runs out, because a stale Alert wakes more panic than none.
_Avoid_: send attempt, outbox message, notification record
