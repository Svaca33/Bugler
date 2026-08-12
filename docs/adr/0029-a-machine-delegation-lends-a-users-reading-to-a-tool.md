---
status: accepted
---

# A Machine Delegation lends a User's reading to a tool and mints no new identity

An MCP client is a process on somebody's laptop, and it has to say who it is. The credential that
looks ready for the job is not: an API Key is Registry's, it belongs to a Service, it *admits
telemetry and reads nothing*, and a Service has no Visibility Scope to lend — asking one what a
person may see is the ambiguity ADR 0006 exists to remove. Reading in Bugler is a fact about a
**User**: Application Grants decide it and `ScopeResolver` enforces it on every query. So the thing
a machine holds is a **Machine Delegation** — that same User's reading, lent to a tool in their name.
It is proven by a Secret shown once at issue (prefix `blgrd_`, distinct from `blgr_` because the two
open opposite doors and a leak should be recognisable as what it is; only its SHA-256 is stored),
it may be narrowed to one Application but never widened past its User's Visibility Scope, it
reads and does not write (see the revision below), it expires (90 days by default), and the User
behind it is read back on every request — so Deactivation and Deletion end it at once, while a
Password Change does not, because unlike a Session it was never minted from a password.

The narrowing, the grade and the span are **stamped in at issue and cannot be edited**. That is what
keeps it a credential rather than a setting: wanting different ones means revoking this one and
issuing another, exactly as with an API Key. A screen that let them be changed would make the same
string mean something new tomorrow, and every tool already holding it would silently gain or lose
reach.

**Revised for the machine hand** (Alerting ADR 0010): a Machine Delegation writes nothing *but* the
machine hand's narrow Alerting verbs — claim, note, propose Solved, resign — and only when the
**grade** stamped into it at issue says so; the default grade keeps today's behaviour, reading
alone. Solved stays a human verdict by construction, now with the construction stated: the machine
may propose it and can do no more, because no verb that renders a verdict exists behind the door.

## Considered Options

- **Let API Keys read.** Two answers to "who is asking" on one credential, and the wrong context
  answering the question — a key that authenticates a sender would have to be told what a person
  may see.
- **OAuth 2.1, as the MCP specification asks of remote servers.** Correct by the protocol, and it
  demands an authorization server of a product whose entire identity story is one cookie and one
  password.
- **A local proxy holding a cookie Session.** No server change at all, but it ships a second
  binary to install and a Session that expires mid-debugging with nothing to renew it against.
- **A general read credential for the whole REST API.** Tempting because "a read is a read" — and
  it would turn the SPA's private contract into a public one: capped counts, cursor pagination and
  UI-shaped DTOs frozen the day somebody's script depends on them. The MCP tool set is the contract
  instead (ADR 0031). A public read API remains available as a decision, to be made on its own
  merits rather than as a side effect of this one.

## Consequences

- Access owns the concept, beside Session, and defines the scheme that authenticates it — it is
  already the only context that calls `AddAuthentication`.
- Revocation costs nothing where it matters most: withdrawing an Application Grant narrows what the
  machine sees on its next query, because `ScopeResolver` asks Access each time; deactivating or
  deleting the User ends the Machine Delegation outright, on the same rule that already ends a Session.
- **No human verb ever travels this way.** Acknowledge, Solve and Quiet Window are not "unmapped
  for now" — they do not exist behind the door, so Solved stays a human verdict by construction
  rather than by restraint. What does travel, behind the machine-hand grade alone, is the machine
  hand's own verbs (Alerting ADR 0010) — marks a machine lays about its own work, never a verdict
  about anyone's.
- A User issues their own without an Admin's approval, because there is nothing to approve: it
  cannot reach past what they already hold. The Admin's leverage is elsewhere and stronger — the
  server switch, the unrouted port (ADR 0030), and sight of every Machine Delegation issued, any of which
  they may revoke. Whoever holds the switch has to be able to see what it opened.
- Expiry buys its safety with a live failure: a Machine Delegation dies while somebody is debugging and an
  agent is holding a 401. The refusal therefore says what happened and what to do, in machine-facing
  English outside the catalogues (ADR 0024), and every Machine Delegation records when it was last used —
  without that, a list of them only grows and is never kept.
