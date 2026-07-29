---
status: accepted
---

# Episodes over cooldowns and digests

When an application starts failing it floods error logs, so "notify on error" naively means hundreds of mails. The classic remedies are a cooldown (suppress for N minutes after each alert) or a digest (periodic summary instead of immediate notice). Both were rejected: a cooldown has no notion of recovery — a two-hour outage re-alerts every time the timer lapses and nobody is ever told it ended — and a digest defeats the founding requirement, knowing *when trouble starts*. Instead, spam is solved structurally by the Episode: the first matching Log Record of a Service opens one and sends the Alert; while it stays open, further matches are only counted; when a Quiet Window passes without a match it closes and sends the All Clear. An outage is exactly two messages, whether it produced ten errors or a hundred thousand.

The detection threshold is fixed at one log — no "N errors in M minutes" knob. Sensitivity (Off / Errors / Errors + Warnings) is the sensitivity control; delaying detection to fill a window would trade blindness time for a problem the Episode already solves. Both Alert and All Clear are always sent, no opt-outs: two messages per outage is not spam, and an outage without a recorded end leaves the reader guessing.

An Episode belongs to one Service, never to an Application. Each Service runs its own deployed version, so "what is failing" is meaningless without saying which Service; a Service resolving at 10:00 must not wait for a sibling that drips errors until 14:00; and one perpetually noisy Service must never silence alerting for its whole Application.

## Consequences

- A Service that drips matching logs forever holds one Episode open forever: one Alert total, never an All Clear — and new, worse trouble inside that open Episode goes unannounced. Accepted for now; the remedy, if ever needed, is escalation on a volume spike within an open Episode, not a different model.
- A cascading application failure opens one Episode per affected Service and fans out that many Alerts at once. Accepted as a true picture; if it ever hurts, the fix is grouping *notifications*, never merging Episodes.
- Episodes are records of outages, not of logs: they survive the retention Purge of the Log Records that drove them (the "first log" link may go dark) and are removed only by the Deletion of their Service or Application.
- Turning Sensitivity Off closes the Service's open Episode immediately and silently — an All Clear would falsely announce a resolution when only the watching stopped.
